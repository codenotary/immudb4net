/*
Copyright 2022 CodeNotary, Inc. All rights reserved.

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

	http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.
*/

namespace ImmuDB;

using Google.Protobuf;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using ImmuDB.Crypto;
using ImmuDB.Exceptions;
using ImmudbProxy;
using Org.BouncyCastle.Crypto;

public partial class ImmuClient
{
    internal const string AUTH_HEADER = "authorization";

    private readonly AsymmetricKeyParameter? serverSigningKey;
    private readonly ImmuStateHolder stateHolder;

    public int ConnectionShutdownTimeoutInSec { get; internal set; }
    private string currentDb = "defaultdb";
    public string GrpcAddress { get; }
    private static LibraryWideSettings globalSettings = new LibraryWideSettings();
    public static LibraryWideSettings GlobalSettings
    {
        get
        {
            return globalSettings;
        }
    }

    internal ImmuService.ImmuServiceClient Service { get { return Connection.Service; } }
    internal IConnection Connection { get; set; }
    internal IConnectionPool ConnectionPool { get; set; }
    internal ISessionManager SessionManager { get; set; }
    internal Session? session;
    private TimeSpan heartbeatInterval;
    private ManualResetEvent? heartbeatCloseRequested;
    internal ManualResetEvent? heartbeatCalled;
    private Task? heartbeatTask;


    public static ImmuClientBuilder NewBuilder()
    {
        return new ImmuClientBuilder();
    }

    public class LibraryWideSettings
    {
        public int MaxConnectionsPerServer { get; set; }
        internal LibraryWideSettings()
        {
            //this is the default value
            MaxConnectionsPerServer = 2;
        }
    }

    public ImmuClient() : this(NewBuilder())
    {

    }

    public ImmuClient(string serverUrl, int serverPort)
        : this(NewBuilder().WithServerUrl(serverUrl).WithServerPort(serverPort))
    {
    }

    public ImmuClient(string serverUrl, int serverPort, string database)
        : this(NewBuilder().WithServerUrl(serverUrl).WithServerPort(serverPort).WithDatabase(database))
    {
    }

    internal ImmuClient(ImmuClientBuilder builder)
    {
        ConnectionPool = builder.ConnectionPool;
        GrpcAddress = builder.GrpcAddress;
        Connection = new ReleasedConnection(ConnectionPool);
        SessionManager = builder.SessionManager;
        serverSigningKey = builder.ServerSigningKey;
        stateHolder = builder.StateHolder;
        heartbeatInterval = builder.HeartbeatInterval;
        ConnectionShutdownTimeoutInSec = builder.ConnectionShutdownTimeoutInSec;
        stateHolder.Key = GrpcAddress.Replace(":", "_").Replace("/", "_").Replace("-", "_");
        stateHolder.Init();
    }

    public static async Task ReleaseSdkResources()
    {
        await RandomAssignConnectionPool.Instance.Shutdown();
    }

    private void StartHeartbeat()
    {
        heartbeatTask = Task.Factory.StartNew(async () =>
        {
            while (true)
            {
                if (heartbeatCloseRequested != null && heartbeatCloseRequested.WaitOne(heartbeatInterval)) return;
                try
                {
                    await Service.WithHeaders(session).KeepAliveAsync(new Empty(), Service.Headers);
                    heartbeatCalled?.Set();
                }
                catch (RpcException) { }
            }
        });
    }

    private void StopHeartbeat()
    {
        if (heartbeatTask == null)
        {
            return;
        }
        heartbeatCloseRequested?.Set();
        heartbeatTask.Wait();
        heartbeatCloseRequested?.Close();
        heartbeatCalled?.Close();
        heartbeatTask = null;
    }


    public async Task Open(string username, string password, string defaultdb)
    {
        if (session != null)
        {
            throw new InvalidOperationException("please close the existing session before opening a new one");
        }
        Connection = ConnectionPool.Acquire(this);
        session = await SessionManager.OpenSession(Connection, username, password, defaultdb);
        heartbeatCloseRequested = new ManualResetEvent(false);
        heartbeatCalled = new ManualResetEvent(false);
        StartHeartbeat();
    }

    public void Reconnect()
    {
        Connection.Pool.Release(this);
        Connection = ConnectionPool.Acquire(this);
    }

    public async Task Close()
    {
        if (Connection == null)
        {
            return;
        }
        StopHeartbeat();
        await SessionManager.CloseSession(Connection, session);
        session = null;
        Connection.Pool.Release(this);
    }

    public bool IsShutdown()
    {
        lock (this)
        {
            return Connection == null;
        }
    }

    private void CheckSessionHasBeenOpen()
    {
        if (session == null)
        {
            throw new ArgumentException("Session is null. Make sure you call Open beforehand.");
        }
    }

    private object stateSync = new Object();

    public ImmuState State()
    {
        lock (stateSync)
        {
            ImmuState? state = stateHolder.GetState(session, currentDb);
            if (state == null)
            {
                state = CurrentState();
                stateHolder.SetState(session!, state);
            }
            else
            {
                CheckSessionHasBeenOpen();
            }
            return state;
        }
    }

    /**
    * Get the current database state that exists on the server.
    * It may throw a RuntimeException if server's state signature verification fails
    * (if this feature is enabled on the client side, at least).
    */
    public ImmuState CurrentState()
    {
        CheckSessionHasBeenOpen();
        ImmudbProxy.ImmutableState state = Service.WithHeaders(session).CurrentState(new Empty(), Service.Headers);
        ImmuState immuState = ImmuState.ValueOf(state);
        if (!immuState.CheckSignature(serverSigningKey))
        {
            throw new VerificationException("State signature verification failed");
        }
        return immuState;
    }

    //
    // ========== DATABASE ==========
    //

    public async Task CreateDatabase(string database)
    {
        CheckSessionHasBeenOpen();
        ImmudbProxy.CreateDatabaseRequest db = new ImmudbProxy.CreateDatabaseRequest
        {
            Name = database
        };

        await Service.WithHeaders(session).CreateDatabaseV2Async(db, Service.Headers);
    }

    public async Task UseDatabase(string database)
    {
        CheckSessionHasBeenOpen();
        ImmudbProxy.Database db = new ImmudbProxy.Database
        {
            DatabaseName = database
        };
        ImmudbProxy.UseDatabaseReply response = await Service.WithHeaders(session).UseDatabaseAsync(db, Service.Headers);
        currentDb = database;
    }

    public async Task<List<string>> Databases()
    {
        CheckSessionHasBeenOpen();
        ImmudbProxy.DatabaseListRequestV2 req = new ImmudbProxy.DatabaseListRequestV2();
        ImmudbProxy.DatabaseListResponseV2 res = await Service.WithHeaders(session).DatabaseListV2Async(req, Service.Headers);
        List<string> list = new List<string>(res.Databases.Count);
        foreach (ImmudbProxy.DatabaseWithSettings db in res.Databases)
        {
            list.Add(db.Name);
        }
        return list;
    }

    //
    // ========== GET ==========
    //

    public async Task<Entry> GetAtTx(byte[] key, ulong tx)
    {
        CheckSessionHasBeenOpen();
        ImmudbProxy.KeyRequest req = new ImmudbProxy.KeyRequest
        {
            Key = Utils.ToByteString(key),
            AtTx = tx
        };
        try
        {
            ImmudbProxy.Entry entry = await Service.WithHeaders(session).GetAsync(req, Service.Headers);
            return Entry.ValueOf(entry);
        }
        catch (RpcException e)
        {
            if (e.Message.Contains("key not found"))
            {
                throw new KeyNotFoundException();
            }

            throw e;
        }
    }

    public async Task<Entry> Get(string key, ulong tx)
    {
        return await GetAtTx(Utils.ToByteArray(key), tx);
    }

    public async Task<Entry> Get(string key)
    {
        return await GetAtTx(Utils.ToByteArray(key), 0);
    }

    public async Task<Entry> VerifiedGet(ImmudbProxy.KeyRequest keyReq, ImmuState state)
    {
        CheckSessionHasBeenOpen();
        ImmudbProxy.VerifiableGetRequest vGetReq = new ImmudbProxy.VerifiableGetRequest()
        {
            KeyRequest = keyReq,
            ProveSinceTx = state.TxId
        };

        ImmudbProxy.VerifiableEntry vEntry;

        try
        {
            vEntry = await Service.WithHeaders(session).VerifiableGetAsync(vGetReq, Service.Headers);
        }
        catch (RpcException e)
        {
            if (e.Message.Contains("key not found"))
            {
                throw new KeyNotFoundException(string.Format("The key {0} was not found", keyReq.Key.ToStringUtf8()));
            }

            throw e;
        }

        ImmuDB.Crypto.InclusionProof inclusionProof = ImmuDB.Crypto.InclusionProof.ValueOf(vEntry.InclusionProof);
        ImmuDB.Crypto.DualProof dualProof = ImmuDB.Crypto.DualProof.ValueOf(vEntry.VerifiableTx.DualProof);

        byte[] eh;
        ulong sourceId, targetId;
        byte[] sourceAlh;
        byte[] targetAlh;

        Entry entry = Entry.ValueOf(vEntry.Entry);

        if (entry.ReferencedBy == null && !keyReq.Key.ToByteArray().SequenceEqual(entry.Key))
        {
            throw new VerificationException("Data is corrupted: entry does not belong to specified key");
        }

        if (entry.ReferencedBy != null && !keyReq.Key.ToByteArray().SequenceEqual(entry.ReferencedBy.Key))
        {
            throw new VerificationException("Data is corrupted: entry does not belong to specified key");
        }

        if (entry.Metadata != null && entry.Metadata.Deleted())
        {
            throw new VerificationException("Data is corrupted: entry is marked as deleted");
        }

        if (keyReq.AtTx != 0 && entry.Tx != keyReq.AtTx)
        {
            throw new VerificationException("Data is corrupted: entry does not belong to specified tx");
        }

        if (state.TxId <= entry.Tx)
        {
            byte[] digest = vEntry.VerifiableTx.DualProof.TargetTxHeader.EH.ToByteArray();
            eh = CryptoUtils.DigestFrom(digest);
            sourceId = state.TxId;
            sourceAlh = CryptoUtils.DigestFrom(state.TxHash);
            targetId = entry.Tx;
            targetAlh = dualProof.TargetTxHeader.Alh();
        }
        else
        {
            byte[] digest = vEntry.VerifiableTx.DualProof.SourceTxHeader.EH.ToByteArray();
            eh = CryptoUtils.DigestFrom(digest);
            sourceId = entry.Tx;
            sourceAlh = dualProof.SourceTxHeader.Alh();
            targetId = state.TxId;
            targetAlh = CryptoUtils.DigestFrom(state.TxHash);
        }

        byte[] kvDigest = entry.DigestFor(vEntry.VerifiableTx.Tx.Header.Version);
        if (!CryptoUtils.VerifyInclusion(inclusionProof, kvDigest, eh))
        {
            throw new VerificationException("Inclusion verification failed.");
        }

        if (state.TxId > 0)
        {
            if (!CryptoUtils.VerifyDualProof(
                    dualProof,
                    sourceId,
                    targetId,
                    sourceAlh,
                    targetAlh
            ))
            {
                throw new VerificationException("Dual proof verification failed.");
            }
        }

        ImmuState newState = new ImmuState(
                currentDb,
                targetId,
                targetAlh,
                (vEntry.VerifiableTx.Signature ?? ImmudbProxy.Signature.DefaultInstance).Signature_.ToByteArray());

        if (!newState.CheckSignature(serverSigningKey))
        {
            throw new VerificationException("State signature verification failed");
        }

        lock (stateSync)
        {
            stateHolder.SetState(session!, newState);
        }
        return Entry.ValueOf(vEntry.Entry);
    }

    public async Task<Entry> VerifiedGet(string key)
    {
        return await VerifiedGetAtTx(key, 0);
    }

    public async Task<Entry> VerifiedGet(byte[] key)
    {
        return await VerifiedGetAtTx(key, 0);
    }

    public async Task<Entry> VerifiedGetAtTx(string key, ulong tx)
    {
        return await VerifiedGetAtTx(Utils.ToByteArray(key), tx);
    }

    public async Task<Entry> VerifiedGetAtTx(byte[] key, ulong tx)
    {
        ImmudbProxy.KeyRequest keyReq = new ImmudbProxy.KeyRequest()
        {
            Key = Utils.ToByteString(key),
            AtTx = tx
        };

        return await VerifiedGet(keyReq, State());
    }

    public async Task<Entry> VerifiedGetSinceTx(string key, ulong tx)
    {
        return await VerifiedGetSinceTx(Utils.ToByteArray(key), tx);
    }

    public async Task<Entry> VerifiedGetSinceTx(byte[] key, ulong tx)
    {
        ImmudbProxy.KeyRequest keyReq = new ImmudbProxy.KeyRequest()
        {
            Key = Utils.ToByteString(key),
            SinceTx = tx
        };

        return await VerifiedGet(keyReq, State());
    }

    public async Task<Entry> VerifiedGetAtRevision(string key, long rev)
    {
        return await VerifiedGetAtRevision(Utils.ToByteArray(key), rev);
    }

    public async Task<Entry> VerifiedGetAtRevision(byte[] key, long rev)
    {
        ImmudbProxy.KeyRequest keyReq = new ImmudbProxy.KeyRequest()
        {
            Key = Utils.ToByteString(key),
            AtRevision = rev
        };

        return await VerifiedGet(keyReq, State());
    }

    public async Task<Entry> GetSinceTx(string key, ulong tx)
    {
        return await GetSinceTx(Utils.ToByteArray(key), tx);
    }

    public async Task<Entry> GetSinceTx(byte[] key, ulong tx)
    {
        CheckSessionHasBeenOpen();
        ImmudbProxy.KeyRequest req = new ImmudbProxy.KeyRequest()
        {
            Key = Utils.ToByteString(key),
            SinceTx = tx
        };

        try
        {
            return Entry.ValueOf(await Service.WithHeaders(session).GetAsync(req, Service.Headers));
        }
        catch (RpcException e)
        {
            if (e.Message.Contains("key not found"))
            {
                throw new KeyNotFoundException();
            }

            throw e;
        }
    }

    public async Task<Entry> GetAtRevision(string key, long rev)
    {
        return await GetAtRevision(Utils.ToByteArray(key), rev);
    }

    public async Task<Entry> GetAtRevision(byte[] key, long rev)
    {
        CheckSessionHasBeenOpen();
        ImmudbProxy.KeyRequest req = new ImmudbProxy.KeyRequest()
        {
            Key = Utils.ToByteString(key),
            AtRevision = rev
        };

        try
        {
            return Entry.ValueOf(await Service.WithHeaders(session).GetAsync(req, Service.Headers));
        }
        catch (RpcException e)
        {
            if (e.Message.Contains("key not found"))
            {
                throw new KeyNotFoundException();
            }

            throw e;
        }
    }

    public async Task<List<Entry>> GetAll(List<string> keys)
    {
        CheckSessionHasBeenOpen();
        List<ByteString> keysBS = new List<ByteString>(keys.Count);

        foreach (string key in keys)
        {
            keysBS.Add(Utils.ToByteString(key));
        }

        ImmudbProxy.KeyListRequest req = new ImmudbProxy.KeyListRequest();
        req.Keys.AddRange(keysBS);

        ImmudbProxy.Entries entries = await Service.WithHeaders(session).GetAllAsync(req, Service.Headers);
        List<Entry> result = new List<Entry>(entries.Entries_.Count);

        foreach (ImmudbProxy.Entry entry in entries.Entries_)
        {
            result.Add(Entry.ValueOf(entry));
        }

        return result;
    }

    //
    // ========== SCAN ==========
    //

    public async Task<List<Entry>> Scan(byte[] prefix, byte[] seekKey, byte[] endKey, bool inclusiveSeek, bool inclusiveEnd,
                            ulong limit, bool desc)
    {
        CheckSessionHasBeenOpen();
        ImmudbProxy.ScanRequest req = new ImmudbProxy.ScanRequest()
        {
            Prefix = Utils.ToByteString(prefix),
            SeekKey = Utils.ToByteString(seekKey),
            EndKey = Utils.ToByteString(endKey),
            InclusiveSeek = inclusiveSeek,
            InclusiveEnd = inclusiveEnd,
            Limit = limit,
            Desc = desc
        };

        ImmudbProxy.Entries entries = await Service.WithHeaders(session).ScanAsync(req, Service.Headers);
        return BuildList(entries);
    }

    public async Task<List<Entry>> Scan(string prefix)
    {
        return await Scan(Utils.ToByteArray(prefix));
    }

    public async Task<List<Entry>> Scan(byte[] prefix)
    {
        return await Scan(prefix, 0, false);
    }

    public async Task<List<Entry>> Scan(string prefix, ulong limit, bool desc)
    {
        return await Scan(Utils.ToByteArray(prefix), limit, desc);
    }

    public async Task<List<Entry>> Scan(byte[] prefix, ulong limit, bool desc)
    {
        return await Scan(prefix, new byte[0], limit, desc);
    }

    public async Task<List<Entry>> Scan(string prefix, string seekKey, ulong limit, bool desc)
    {
        return await Scan(Utils.ToByteArray(prefix), Utils.ToByteArray(seekKey), limit, desc);
    }

    public async Task<List<Entry>> Scan(string prefix, string seekKey, string endKey, ulong limit, bool desc)
    {
        return await Scan(Utils.ToByteArray(prefix), Utils.ToByteArray(seekKey), Utils.ToByteArray(endKey), limit, desc);
    }

    public async Task<List<Entry>> Scan(byte[] prefix, byte[] seekKey, ulong limit, bool desc)
    {
        return await Scan(prefix, seekKey, new byte[0], limit, desc);
    }

    public async Task<List<Entry>> Scan(byte[] prefix, byte[] seekKey, byte[] endKey, ulong limit, bool desc)
    {
        return await Scan(prefix, seekKey, endKey, false, false, limit, desc);
    }

    //
    // ========== SET ==========
    //

    public async Task<TxHeader> Set(byte[] key, byte[] value)
    {
        CheckSessionHasBeenOpen();
        ImmudbProxy.KeyValue kv = new ImmudbProxy.KeyValue()
        {
            Key = Utils.ToByteString(key),
            Value = Utils.ToByteString(value)
        };

        ImmudbProxy.SetRequest req = new ImmudbProxy.SetRequest();
        req.KVs.Add(kv);

        ImmudbProxy.TxHeader txHdr = await Service.WithHeaders(session).SetAsync(req, Service.Headers);

        if (txHdr.Nentries != 1)
        {
            throw new CorruptedDataException();
        }

        return TxHeader.ValueOf(txHdr);
    }

    public async Task<TxHeader> Set(string key, byte[] value)
    {
        return await Set(Utils.ToByteArray(key), value);
    }

    public async Task<TxHeader> Set(string key, string value)
    {
        return await Set(Utils.ToByteArray(key), Utils.ToByteArray(value));
    }

    public async Task<TxHeader> SetAll(List<KVPair> kvList)
    {
        CheckSessionHasBeenOpen();
        ImmudbProxy.SetRequest request = new ImmudbProxy.SetRequest();
        foreach (KVPair kv in kvList)
        {
            ImmudbProxy.KeyValue kvProxy = new ImmudbProxy.KeyValue();
            kvProxy.Key = Utils.ToByteString(kv.Key);
            kvProxy.Value = Utils.ToByteString(kv.Value);
            request.KVs.Add(kvProxy);
        }

        ImmudbProxy.TxHeader txHdr = await Service.WithHeaders(session).SetAsync(request, Service.Headers);

        if (txHdr.Nentries != kvList.Count)
        {
            throw new CorruptedDataException();
        }

        return TxHeader.ValueOf(txHdr);
    }

    public async Task<TxHeader> SetReference(byte[] key, byte[] referencedKey, ulong atTx)
    {
        CheckSessionHasBeenOpen();
        ImmudbProxy.ReferenceRequest req = new ImmudbProxy.ReferenceRequest()
        {
            Key = Utils.ToByteString(key),
            ReferencedKey = Utils.ToByteString(referencedKey),
            AtTx = atTx,
            BoundRef = atTx > 0
        };

        ImmudbProxy.TxHeader txHdr = await Service.WithHeaders(session).SetReferenceAsync(req, Service.Headers);

        if (txHdr.Nentries != 1)
        {
            throw new CorruptedDataException();
        }

        return TxHeader.ValueOf(txHdr);
    }

    public async Task<TxHeader> SetReference(string key, string referencedKey, ulong atTx)
    {
        return await SetReference(
            Utils.ToByteArray(key),
            Utils.ToByteArray(referencedKey),
            atTx);
    }

    public async Task<TxHeader> SetReference(string key, string referencedKey)
    {
        return await SetReference(
            Utils.ToByteArray(key),
            Utils.ToByteArray(referencedKey),
            0);
    }

    public async Task<TxHeader> SetReference(byte[] key, byte[] referencedKey)
    {
        return await SetReference(key, referencedKey, 0);
    }

    public async Task<TxHeader> VerifiedSet(string key, byte[] value)
    {
        return await VerifiedSet(Utils.ToByteArray(key), value);
    }

    public async Task<TxHeader> VerifiedSet(string key, string value)
    {
        return await VerifiedSet(Utils.ToByteArray(key), Utils.ToByteArray(value));
    }

    public async Task<TxHeader> VerifiedSet(byte[] key, byte[] value)
    {
        CheckSessionHasBeenOpen();

        ImmuState state = State();
        ImmudbProxy.KeyValue kv = new ImmudbProxy.KeyValue()
        {
            Key = Utils.ToByteString(key),
            Value = Utils.ToByteString(value),
        };

        var setRequest = new ImmudbProxy.SetRequest();
        setRequest.KVs.Add(kv);
        ImmudbProxy.VerifiableSetRequest vSetReq = new ImmudbProxy.VerifiableSetRequest()
        {
            SetRequest = setRequest,
            ProveSinceTx = state.TxId
        };

        // using the awaitable VerifiableSetAsync is not ok here, because in the multithreading case it fails. Switched back to synchronous call in this case.
        ImmudbProxy.VerifiableTx vtx;
        lock (this)
        {
            vtx = Service.WithHeaders(session).VerifiableSet(vSetReq, Service.Headers);
        }
        await Task.Yield();

        int ne = vtx.Tx.Header.Nentries;

        if (ne != 1 || vtx.Tx.Entries.Count != 1)
        {
            throw new VerificationException($"Got back {ne} entries (in tx metadata) instead of 1.");
        }

        Tx tx;
        try
        {
            tx = Tx.ValueOf(vtx.Tx);
        }
        catch (Exception e)
        {
            throw new VerificationException("Failed to extract the transaction.", e);
        }

        TxHeader txHeader = tx.Header;

        Entry entry = Entry.ValueOf(new ImmudbProxy.Entry()
        {
            Key = Utils.ToByteString(key),
            Value = Utils.ToByteString(value),
        });

        ImmuDB.Crypto.InclusionProof inclusionProof = tx.Proof(entry.getEncodedKey());

        if (!CryptoUtils.VerifyInclusion(inclusionProof, entry.DigestFor(txHeader.Version), txHeader.Eh))
        {
            throw new VerificationException("Data is corrupted (verify inclusion failed)");
        }

        ImmuState newState = VerifyDualProof(vtx, tx, state);

        if (!newState.CheckSignature(serverSigningKey))
        {
            throw new VerificationException("State signature verification failed");
        }

        lock (stateSync)
        {
            stateHolder.SetState(session!, newState);
        }
        return TxHeader.ValueOf(vtx.Tx.Header);
    }

    private ImmuState VerifyDualProof(ImmudbProxy.VerifiableTx vtx, Tx tx, ImmuState state)
    {
        ulong sourceId = state.TxId;
        ulong targetId = tx.Header.Id;
        byte[] sourceAlh = CryptoUtils.DigestFrom(state.TxHash);
        byte[] targetAlh = tx.Header.Alh();

        if (state.TxId > 0)
        {
            if (!CryptoUtils.VerifyDualProof(
                    Crypto.DualProof.ValueOf(vtx.DualProof),
                    sourceId,
                    targetId,
                    sourceAlh,
                    targetAlh
            ))
            {
                throw new VerificationException("Data is corrupted (dual proof verification failed).");
            }
        }

        return new ImmuState(currentDb, targetId, targetAlh, (vtx.Signature ?? ImmudbProxy.Signature.DefaultInstance).Signature_.ToByteArray());
    }

    //
    // ========== Z ==========
    //

    public async Task<TxHeader> ZAdd(byte[] set, byte[] key, ulong atTx, double score)
    {
        CheckSessionHasBeenOpen();
        ImmudbProxy.TxHeader txHdr = await Service.WithHeaders(session).ZAddAsync(
                new ImmudbProxy.ZAddRequest()
                {
                    Set = Utils.ToByteString(set),
                    Key = Utils.ToByteString(key),
                    AtTx = atTx,
                    Score = score,
                    BoundRef = atTx > 0
                }, Service.Headers);

        if (txHdr.Nentries != 1)
        {
            throw new CorruptedDataException();
        }

        return TxHeader.ValueOf(txHdr);
    }

    public async Task<TxHeader> ZAdd(string set, string key, double score)
    {
        return await ZAdd(Utils.ToByteArray(set), Utils.ToByteArray(key), score);
    }

    public async Task<TxHeader> ZAdd(byte[] set, byte[] key, double score)
    {
        return await ZAdd(set, key, 0, score);
    }

    public async Task<TxHeader> VerifiedZAdd(byte[] set, byte[] key, ulong atTx, double score)
    {
        CheckSessionHasBeenOpen();

        ImmuState state = State();
        ImmudbProxy.ZAddRequest zAddReq = new ImmudbProxy.ZAddRequest()
        {
            Set = Utils.ToByteString(set),
            Key = Utils.ToByteString(key),
            AtTx = atTx,
            Score = score,
            BoundRef = atTx > 0
        };
        ImmudbProxy.VerifiableZAddRequest vZAddReq = new ImmudbProxy.VerifiableZAddRequest()
        {
            ZAddRequest = zAddReq,
            ProveSinceTx = state.TxId
        };

        ImmudbProxy.VerifiableTx vtx = await Service.WithHeaders(session).VerifiableZAddAsync(vZAddReq, Service.Headers);

        if (vtx.Tx.Header.Nentries != 1)
        {
            throw new VerificationException("Data is corrupted.");
        }

        Tx tx;
        try
        {
            tx = Tx.ValueOf(vtx.Tx);
        }
        catch (Exception e)
        {
            throw new VerificationException("Failed to extract the transaction.", e);
        }

        TxHeader txHeader = tx.Header;

        ZEntry entry = ZEntry.ValueOf(new ImmudbProxy.ZEntry
        {
            Set = Utils.ToByteString(set),
            Key = Utils.ToByteString(key),
            AtTx = atTx,
            Score = score
        });

        Crypto.InclusionProof inclusionProof = tx.Proof(entry.getEncodedKey());

        if (!CryptoUtils.VerifyInclusion(inclusionProof, entry.DigestFor(txHeader.Version), txHeader.Eh))
        {
            throw new VerificationException("Data is corrupted (inclusion verification failed).");
        }

        if (!txHeader.Eh.SequenceEqual(CryptoUtils.DigestFrom(vtx.DualProof.TargetTxHeader.EH.ToByteArray())))
        {
            throw new VerificationException("Data is corrupted (different digests).");
        }

        ImmuState newState = VerifyDualProof(vtx, tx, state);

        if (!newState.CheckSignature(serverSigningKey))
        {
            throw new VerificationException("State signature verification failed");
        }
        lock (stateSync)
        {
            stateHolder.SetState(session!, newState);
        }
        return TxHeader.ValueOf(vtx.Tx.Header);
    }

    public async Task<TxHeader> VerifiedZAdd(string set, string key, double score)
    {
        return await VerifiedZAdd(Utils.ToByteArray(set), Utils.ToByteArray(key), score);
    }

    public async Task<TxHeader> VerifiedZAdd(byte[] set, byte[] key, double score)
    {
        return await VerifiedZAdd(set, key, 0, score);
    }

    public async Task<TxHeader> VerifiedZAdd(string set, string key, ulong atTx, double score)
    {
        return await VerifiedZAdd(Utils.ToByteArray(set), Utils.ToByteArray(key), atTx, score);
    }

    public async Task<List<ZEntry>> ZScan(string set, ulong limit, bool reverse)
    {
        return await ZScan(Utils.ToByteArray(set), limit, reverse);
    }

    public async Task<List<ZEntry>> ZScan(byte[] set, ulong limit, bool reverse)
    {
        CheckSessionHasBeenOpen();
        ImmudbProxy.ZScanRequest req = new ImmudbProxy.ZScanRequest()
        {
            Set = Utils.ToByteString(set),
            Limit = limit,
            Desc = reverse
        };

        ImmudbProxy.ZEntries zEntries = await Service.WithHeaders(session).ZScanAsync(req, Service.Headers);
        return BuildList(zEntries);
    }

    //
    // ========== DELETE ==========
    //

    public async Task<TxHeader> Delete(string key)
    {
        return await Delete(Utils.ToByteArray(key));
    }

    public async Task<TxHeader> Delete(byte[] key)
    {
        CheckSessionHasBeenOpen();
        try
        {
            ImmudbProxy.DeleteKeysRequest req = new ImmudbProxy.DeleteKeysRequest()
            {
                Keys = { Utils.ToByteString(key) }
            };
            return TxHeader.ValueOf(await Service.WithHeaders(session).DeleteAsync(req, Service.Headers));
        }
        catch (RpcException e)
        {
            if (e.Message.Contains("key not found"))
            {

                throw new KeyNotFoundException();
            }

            throw e;
        }
    }

    //
    // ========== TX ==========
    //

    public async Task<Tx> TxById(ulong txId)
    {
        CheckSessionHasBeenOpen();
        try
        {
            ImmudbProxy.Tx tx = await Service.WithHeaders(session).TxByIdAsync(
                new ImmudbProxy.TxRequest()
                {
                    Tx = txId
                }, Service.Headers);
            return Tx.ValueOf(tx);
        }
        catch (RpcException e)
        {
            if (e.Message.Contains("tx not found"))
            {
                throw new TxNotFoundException();
            }

            throw e;
        }
    }

    public async Task<Tx> VerifiedTxById(ulong txId)
    {
        CheckSessionHasBeenOpen();
        ImmuState state = State();
        ImmudbProxy.VerifiableTxRequest vTxReq = new ImmudbProxy.VerifiableTxRequest()
        {
            Tx = txId,
            ProveSinceTx = state.TxId
        };

        ImmudbProxy.VerifiableTx vtx;

        try
        {
            vtx = await Service.WithHeaders(session).VerifiableTxByIdAsync(vTxReq, Service.Headers);
        }
        catch (RpcException e)
        {
            if (e.Message.Contains("tx not found"))
            {
                throw new TxNotFoundException();
            }
            throw e;
        }

        Crypto.DualProof dualProof = Crypto.DualProof.ValueOf(vtx.DualProof);
        ulong sourceId;
        ulong targetId;
        byte[] sourceAlh;
        byte[] targetAlh;

        if (state.TxId <= txId)
        {
            sourceId = state.TxId;
            sourceAlh = CryptoUtils.DigestFrom(state.TxHash);
            targetId = txId;
            targetAlh = dualProof.TargetTxHeader.Alh();
        }
        else
        {
            sourceId = txId;
            sourceAlh = dualProof.SourceTxHeader.Alh();
            targetId = state.TxId;
            targetAlh = CryptoUtils.DigestFrom(state.TxHash);
        }

        if (state.TxId > 0)
        {
            if (!CryptoUtils.VerifyDualProof(
                    Crypto.DualProof.ValueOf(vtx.DualProof),
                    sourceId,
                    targetId,
                    sourceAlh,
                    targetAlh
            ))
            {
                throw new VerificationException("Data is corrupted (dual proof verification failed).");
            }
        }

        Tx tx;
        try
        {
            tx = Tx.ValueOf(vtx.Tx);
        }
        catch (Exception e)
        {
            throw new VerificationException("Failed to extract the transaction.", e);
        }

        ImmuState newState = new ImmuState(currentDb, targetId, targetAlh, (vtx.Signature ?? ImmudbProxy.Signature.DefaultInstance).Signature_.ToByteArray());

        if (!newState.CheckSignature(serverSigningKey))
        {
            throw new VerificationException("State signature verification failed");
        }
        lock (stateSync)
        {
            stateHolder.SetState(session!, newState);
        }
        return tx;
    }

    public async Task<List<Tx>> TxScan(ulong initialTxId)
    {
        CheckSessionHasBeenOpen();
        ImmudbProxy.TxScanRequest req = new ImmudbProxy.TxScanRequest()
        {
            InitialTx = initialTxId
        };

        ImmudbProxy.TxList txList = await Service.WithHeaders(session).TxScanAsync(req, Service.Headers);
        return buildList(txList);
    }

    public async Task<List<Tx>> TxScan(ulong initialTxId, uint limit, bool desc)
    {
        ImmudbProxy.TxScanRequest req = new ImmudbProxy.TxScanRequest()
        {
            InitialTx = initialTxId,
            Limit = limit,
            Desc = desc
        };
        ImmudbProxy.TxList txList = await Service.WithHeaders(session).TxScanAsync(req, Service.Headers);
        return buildList(txList);
    }

    //
    // ========== HEALTH ==========
    //

    public async Task<bool> HealthCheck()
    {
        var healthResponse = await Service.WithHeaders(session).HealthAsync(new Empty(), Service.Headers);
        return healthResponse.Status;
    }

    public bool IsConnected()
    {
        return Connection != null;
    }

    //
    // ========== USER MGMT ==========
    //

    public async Task<List<Iam.User>> ListUsers()
    {
        CheckSessionHasBeenOpen();
        ImmudbProxy.UserList userList = await Service.WithHeaders(session).ListUsersAsync(new Empty(), Service.Headers);
        return userList.Users.ToList()
                .Select(u => new Iam.User(
                    u.User_.ToString(System.Text.Encoding.UTF8),
                    BuildPermissions(u.Permissions))
                {
                    Active = u.Active,
                    CreatedAt = u.Createdat,
                    CreatedBy = u.Createdby,
                }).ToList();
    }

    private List<Iam.Permission> BuildPermissions(RepeatedField<ImmudbProxy.Permission> permissionsList)
    {
        return permissionsList.ToList()
                .Select(p => (Iam.Permission)p.Permission_).ToList();
    }

    public async Task CreateUser(string user, string password, Iam.Permission permission, string database)
    {
        CheckSessionHasBeenOpen();
        ImmudbProxy.CreateUserRequest createUserRequest = new ImmudbProxy.CreateUserRequest()
        {
            User = Utils.ToByteString(user),
            Password = Utils.ToByteString(password),
            Permission = (uint)permission,
            Database = database
        };

        await Service.WithHeaders(session).CreateUserAsync(createUserRequest, Service.Headers);
    }

    public async Task ChangePassword(string user, string oldPassword, string newPassword)
    {
        CheckSessionHasBeenOpen();
        ImmudbProxy.ChangePasswordRequest changePasswordRequest = new ImmudbProxy.ChangePasswordRequest()
        {
            User = Utils.ToByteString(user),
            OldPassword = Utils.ToByteString(oldPassword),
            NewPassword = Utils.ToByteString(newPassword),
        };

        await Service.WithHeaders(session).ChangePasswordAsync(changePasswordRequest, Service.Headers);
    }

    //
    // ========== INDEX MGMT ==========
    //

    public async Task FlushIndex(float cleanupPercentage, bool synced)
    {
        CheckSessionHasBeenOpen();
        ImmudbProxy.FlushIndexRequest req = new ImmudbProxy.FlushIndexRequest()
        {
            CleanupPercentage = cleanupPercentage,
            Synced = synced
        };

        await Service.WithHeaders(session).FlushIndexAsync(req, Service.Headers);
    }

    public async Task CompactIndex()
    {
        CheckSessionHasBeenOpen();
        await Service.WithHeaders(session).CompactIndexAsync(new Empty(), Service.Headers);
    }

    //
    // ========== HISTORY ==========
    //

    public async Task<List<Entry>> History(string key, int limit, ulong offset, bool desc)
    {
        return await History(Utils.ToByteArray(key), limit, offset, desc);
    }

    public async Task<List<Entry>> History(byte[] key, int limit, ulong offset, bool desc)
    {
        CheckSessionHasBeenOpen();
        try
        {
            ImmudbProxy.Entries entries = await Service.WithHeaders(session).HistoryAsync(new ImmudbProxy.HistoryRequest()
            {
                Key = Utils.ToByteString(key),
                Limit = limit,
                Offset = offset,
                Desc = desc
            }, Service.Headers);

            return BuildList(entries);
        }
        catch (RpcException e)
        {
            if (e.Message.Contains("key not found"))
            {
                throw new KeyNotFoundException();
            }

            throw e;
        }
    }

    private List<Entry> BuildList(ImmudbProxy.Entries entries)
    {
        List<Entry> result = new List<Entry>(entries.Entries_.Count);
        entries.Entries_.ToList().ForEach(entry => result.Add(Entry.ValueOf(entry)));
        return result;
    }

    private List<ZEntry> BuildList(ImmudbProxy.ZEntries entries)
    {
        List<ZEntry> result = new List<ZEntry>(entries.Entries.Count);
        entries.Entries.ToList()
                .ForEach(entry => result.Add(ZEntry.ValueOf(entry)));
        return result;
    }

    private List<Tx> buildList(ImmudbProxy.TxList txList)
    {
        List<Tx> result = new List<Tx>(txList.Txs.Count);
        txList.Txs.ToList().ForEach(tx =>
        {
            try
            {
                result.Add(Tx.ValueOf(tx));
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        });
        return result;
    }
}