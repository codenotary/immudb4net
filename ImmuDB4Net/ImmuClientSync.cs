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

public partial class ImmuClientSync
{
    internal const string AUTH_HEADER = "authorization";

    private readonly AsymmetricKeyParameter? serverSigningKey;
    private readonly IImmuStateHolder stateHolder;

    public TimeSpan ConnectionShutdownTimeout { get; set; }
    public TimeSpan IdleConnectionCheckInterval { get; internal set; }
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
    internal object connectionSync = new Object();
    private IConnection connection;
    internal IConnection Connection
    {
        get
        {
            lock (connectionSync)
            {
                return connection;
            }
        }

        set
        {
            lock (connectionSync)
            {
                connection = value;
            }
        }
    }
    internal IConnectionPool ConnectionPool { get; set; }
    internal ISessionManager SessionManager { get; set; }
    internal object sessionSync = new Object();
    private Session? activeSession;
    private TimeSpan heartbeatInterval;
    private ManualResetEvent? heartbeatCloseRequested;
    internal ManualResetEvent? heartbeatCalled;
    private Task? heartbeatTask;
    private ReleasedConnection releasedConnection = new ReleasedConnection();
    public bool DeploymentInfoCheck { get; set; } = true;
    internal Session? ActiveSession
    {
        get
        {
            lock (sessionSync)
            {
                return activeSession;
            }
        }
        set
        {
            lock (sessionSync)
            {
                activeSession = value;
            }
        }
    }

    public static ImmuClientSyncBuilder NewBuilder()
    {
        return new ImmuClientSyncBuilder();
    }

    public class LibraryWideSettings
    {
        public int MaxConnectionsPerServer { get; set; }
        public TimeSpan TerminateIdleConnectionTimeout { get; set; }
        public TimeSpan IdleConnectionCheckInterval { get; set; }
        internal LibraryWideSettings()
        {
            //these are the default values
            MaxConnectionsPerServer = 2;
            TerminateIdleConnectionTimeout = TimeSpan.FromSeconds(60);
            IdleConnectionCheckInterval = TimeSpan.FromSeconds(6);
        }
    }

    public ImmuClientSync() : this(NewBuilder())
    {

    }

    public ImmuClientSync(string serverUrl, int serverPort)
        : this(NewBuilder().WithServerUrl(serverUrl).WithServerPort(serverPort))
    {
    }

    public ImmuClientSync(string serverUrl, int serverPort, string database)
        : this(NewBuilder().WithServerUrl(serverUrl).WithServerPort(serverPort).WithDatabase(database))
    {
    }

    internal ImmuClientSync(ImmuClientSyncBuilder builder)
    {
        ConnectionPool = builder.ConnectionPool;
        GrpcAddress = builder.GrpcAddress;
        connection = releasedConnection;
        SessionManager = builder.SessionManager;
        DeploymentInfoCheck = builder.DeploymentInfoCheck;
        serverSigningKey = builder.ServerSigningKey;
        stateHolder = builder.StateHolder;
        heartbeatInterval = builder.HeartbeatInterval;
        ConnectionShutdownTimeout = builder.ConnectionShutdownTimeout;
        stateHolder.DeploymentInfoCheck = builder.DeploymentInfoCheck;
        stateHolder.DeploymentKey = Utils.GenerateShortHash(GrpcAddress);
        stateHolder.DeploymentLabel = GrpcAddress;
    }


    public static void ReleaseSdkResources()
    {
        RandomAssignConnectionPool.Instance.Shutdown();
    }

    private void StartHeartbeat()
    {
        heartbeatTask = Task.Factory.StartNew(async () =>
        {
            while (true)
            {
                if (heartbeatCloseRequested!.WaitOne(heartbeatInterval)) return;
                try
                {
                    await Service.KeepAliveAsync(new Empty(), Service.GetHeaders(ActiveSession));
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

    public void Open(string username, string password, string defaultdb)
    {
        lock (sessionSync)
        {
            if (activeSession != null)
            {
                throw new InvalidOperationException("please close the existing session before opening a new one");
            }
            Connection = ConnectionPool.Acquire(new ConnectionParameters
            {
                Address = GrpcAddress,
                ShutdownTimeout = ConnectionShutdownTimeout
            });
            activeSession = SessionManager.OpenSession(Connection, username, password, defaultdb);
            heartbeatCloseRequested = new ManualResetEvent(false);
            heartbeatCalled = new ManualResetEvent(false);
            StartHeartbeat();
        }
    }

    public void Reconnect()
    {
        lock (connectionSync)
        {
            ConnectionPool.Release(connection);
            connection = ConnectionPool.Acquire(new ConnectionParameters
            {
                Address = GrpcAddress,
                ShutdownTimeout = ConnectionShutdownTimeout
            });
        }
    }

    public void Close()
    {
        lock (sessionSync)
        {
            StopHeartbeat();
            SessionManager.CloseSession(Connection, activeSession);
            activeSession = null;
        }
        lock (connectionSync)
        {
            ConnectionPool.Release(connection);
            connection = releasedConnection;
        }
    }

    public bool IsClosed()
    {
        return Connection.Released;
    }

    private void CheckSessionHasBeenOpened()
    {
        if (ActiveSession == null)
        {
            throw new ArgumentException("Session is null. Make sure you call Open beforehand.");
        }
    }

    private object stateSync = new Object();

    public ImmuState State()
    {
        lock (stateSync)
        {
            ImmuState? state = stateHolder.GetState(ActiveSession, currentDb);
            if (state == null)
            {
                state = CurrentState();
                stateHolder.SetState(ActiveSession!, state);
            }
            else
            {
                CheckSessionHasBeenOpened();
            }
            return state;
        }
    }

    /// <summary>
    /// Get the current database state that exists on the server. It may throw a RuntimeException if server's state signature verification fails.
    /// </summary>
    /// <returns></returns>
    public ImmuState CurrentState()
    {
        CheckSessionHasBeenOpened();
        ImmudbProxy.ImmutableState state = Service.CurrentState(new Empty(), Service.GetHeaders(ActiveSession));
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

    public void CreateDatabase(string database)
    {
        CheckSessionHasBeenOpened();
        ImmudbProxy.CreateDatabaseRequest db = new ImmudbProxy.CreateDatabaseRequest
        {
            Name = database
        };

        Service.CreateDatabaseV2(db, Service.GetHeaders(ActiveSession));
    }

    public void UseDatabase(string database)
    {
        CheckSessionHasBeenOpened();
        ImmudbProxy.Database db = new ImmudbProxy.Database
        {
            DatabaseName = database
        };
        ImmudbProxy.UseDatabaseReply response = Service.UseDatabase(db, Service.GetHeaders(ActiveSession));
        currentDb = database;
    }

    public List<string> Databases()
    {
        CheckSessionHasBeenOpened();
        ImmudbProxy.DatabaseListRequestV2 req = new ImmudbProxy.DatabaseListRequestV2();
        ImmudbProxy.DatabaseListResponseV2 res = Service.DatabaseListV2(req, Service.GetHeaders(ActiveSession));
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

    public Entry GetAtTx(byte[] key, ulong tx)
    {
        CheckSessionHasBeenOpened();
        ImmudbProxy.KeyRequest req = new ImmudbProxy.KeyRequest
        {
            Key = Utils.ToByteString(key),
            AtTx = tx
        };
        try
        {
            ImmudbProxy.Entry entry = Service.Get(req, Service.GetHeaders(ActiveSession));
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

    public Entry Get(string key, ulong tx)
    {
        return GetAtTx(Utils.ToByteArray(key), tx);
    }

    public Entry Get(string key)
    {
        return GetAtTx(Utils.ToByteArray(key), 0);
    }

    public Entry VerifiedGet(ImmudbProxy.KeyRequest keyReq, ImmuState state)
    {
        CheckSessionHasBeenOpened();
        ImmudbProxy.VerifiableGetRequest vGetReq = new ImmudbProxy.VerifiableGetRequest()
        {
            KeyRequest = keyReq,
            ProveSinceTx = state.TxId
        };

        ImmudbProxy.VerifiableEntry vEntry;

        try
        {
            vEntry = Service.VerifiableGet(vGetReq, Service.GetHeaders(ActiveSession));
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

        UpdateState(newState);
        return Entry.ValueOf(vEntry.Entry);
    }

    public Entry VerifiedGet(string key)
    {
        return VerifiedGetAtTx(key, 0);
    }

    public Entry VerifiedGet(byte[] key)
    {
        return VerifiedGetAtTx(key, 0);
    }

    public Entry VerifiedGetAtTx(string key, ulong tx)
    {
        return VerifiedGetAtTx(Utils.ToByteArray(key), tx);
    }

    public Entry VerifiedGetAtTx(byte[] key, ulong tx)
    {
        ImmudbProxy.KeyRequest keyReq = new ImmudbProxy.KeyRequest()
        {
            Key = Utils.ToByteString(key),
            AtTx = tx
        };

        return VerifiedGet(keyReq, State());
    }

    public Entry VerifiedGetSinceTx(string key, ulong tx)
    {
        return VerifiedGetSinceTx(Utils.ToByteArray(key), tx);
    }

    public Entry VerifiedGetSinceTx(byte[] key, ulong tx)
    {
        ImmudbProxy.KeyRequest keyReq = new ImmudbProxy.KeyRequest()
        {
            Key = Utils.ToByteString(key),
            SinceTx = tx
        };

        return VerifiedGet(keyReq, State());
    }

    public Entry VerifiedGetAtRevision(string key, long rev)
    {
        return VerifiedGetAtRevision(Utils.ToByteArray(key), rev);
    }

    public Entry VerifiedGetAtRevision(byte[] key, long rev)
    {
        ImmudbProxy.KeyRequest keyReq = new ImmudbProxy.KeyRequest()
        {
            Key = Utils.ToByteString(key),
            AtRevision = rev
        };

        return VerifiedGet(keyReq, State());
    }

    public Entry GetSinceTx(string key, ulong tx)
    {
        return GetSinceTx(Utils.ToByteArray(key), tx);
    }

    public Entry GetSinceTx(byte[] key, ulong tx)
    {
        CheckSessionHasBeenOpened();
        ImmudbProxy.KeyRequest req = new ImmudbProxy.KeyRequest()
        {
            Key = Utils.ToByteString(key),
            SinceTx = tx
        };

        try
        {
            return Entry.ValueOf(Service.Get(req, Service.GetHeaders(ActiveSession)));
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

    public Entry GetAtRevision(string key, long rev)
    {
        return GetAtRevision(Utils.ToByteArray(key), rev);
    }

    public Entry GetAtRevision(byte[] key, long rev)
    {
        CheckSessionHasBeenOpened();
        ImmudbProxy.KeyRequest req = new ImmudbProxy.KeyRequest()
        {
            Key = Utils.ToByteString(key),
            AtRevision = rev
        };

        try
        {
            return Entry.ValueOf(Service.Get(req, Service.GetHeaders(ActiveSession)));
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

    public List<Entry> GetAll(List<string> keys)
    {
        CheckSessionHasBeenOpened();
        List<ByteString> keysBS = new List<ByteString>(keys.Count);

        foreach (string key in keys)
        {
            keysBS.Add(Utils.ToByteString(key));
        }

        ImmudbProxy.KeyListRequest req = new ImmudbProxy.KeyListRequest();
        req.Keys.AddRange(keysBS);

        ImmudbProxy.Entries entries = Service.GetAll(req, Service.GetHeaders(ActiveSession));
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

    public List<Entry> Scan(byte[] prefix, byte[] seekKey, byte[] endKey, bool inclusiveSeek, bool inclusiveEnd,
                            ulong limit, bool desc)
    {
        CheckSessionHasBeenOpened();
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

        ImmudbProxy.Entries entries = Service.Scan(req, Service.GetHeaders(ActiveSession));
        return BuildList(entries);
    }

    public List<Entry> Scan(string prefix)
    {
        return Scan(Utils.ToByteArray(prefix));
    }

    public List<Entry> Scan(byte[] prefix)
    {
        return Scan(prefix, 0, false);
    }

    public List<Entry> Scan(string prefix, ulong limit, bool desc)
    {
        return Scan(Utils.ToByteArray(prefix), limit, desc);
    }

    public List<Entry> Scan(byte[] prefix, ulong limit, bool desc)
    {
        return Scan(prefix, new byte[0], limit, desc);
    }

    public List<Entry> Scan(string prefix, string seekKey, ulong limit, bool desc)
    {
        return Scan(Utils.ToByteArray(prefix), Utils.ToByteArray(seekKey), limit, desc);
    }

    public List<Entry> Scan(string prefix, string seekKey, string endKey, ulong limit, bool desc)
    {
        return Scan(Utils.ToByteArray(prefix), Utils.ToByteArray(seekKey), Utils.ToByteArray(endKey), limit, desc);
    }

    public List<Entry> Scan(byte[] prefix, byte[] seekKey, ulong limit, bool desc)
    {
        return Scan(prefix, seekKey, new byte[0], limit, desc);
    }

    public List<Entry> Scan(byte[] prefix, byte[] seekKey, byte[] endKey, ulong limit, bool desc)
    {
        return Scan(prefix, seekKey, endKey, false, false, limit, desc);
    }

    //
    // ========== SET ==========
    //

    public TxHeader Set(byte[] key, byte[] value)
    {
        CheckSessionHasBeenOpened();
        ImmudbProxy.KeyValue kv = new ImmudbProxy.KeyValue()
        {
            Key = Utils.ToByteString(key),
            Value = Utils.ToByteString(value)
        };

        ImmudbProxy.SetRequest req = new ImmudbProxy.SetRequest();
        req.KVs.Add(kv);

        ImmudbProxy.TxHeader txHdr = Service.Set(req, Service.GetHeaders(ActiveSession));

        if (txHdr.Nentries != 1)
        {
            throw new CorruptedDataException();
        }

        return TxHeader.ValueOf(txHdr);
    }

    public TxHeader Set(string key, byte[] value)
    {
        return Set(Utils.ToByteArray(key), value);
    }

    public TxHeader Set(string key, string value)
    {
        return Set(Utils.ToByteArray(key), Utils.ToByteArray(value));
    }

    public TxHeader SetAll(List<KVPair> kvList)
    {
        CheckSessionHasBeenOpened();
        ImmudbProxy.SetRequest request = new ImmudbProxy.SetRequest();
        foreach (KVPair kv in kvList)
        {
            ImmudbProxy.KeyValue kvProxy = new ImmudbProxy.KeyValue();
            kvProxy.Key = Utils.ToByteString(kv.Key);
            kvProxy.Value = Utils.ToByteString(kv.Value);
            request.KVs.Add(kvProxy);
        }

        ImmudbProxy.TxHeader txHdr = Service.Set(request, Service.GetHeaders(ActiveSession));

        if (txHdr.Nentries != kvList.Count)
        {
            throw new CorruptedDataException();
        }

        return TxHeader.ValueOf(txHdr);
    }

    public TxHeader SetReference(byte[] key, byte[] referencedKey, ulong atTx)
    {
        CheckSessionHasBeenOpened();
        ImmudbProxy.ReferenceRequest req = new ImmudbProxy.ReferenceRequest()
        {
            Key = Utils.ToByteString(key),
            ReferencedKey = Utils.ToByteString(referencedKey),
            AtTx = atTx,
            BoundRef = atTx > 0
        };

        ImmudbProxy.TxHeader txHdr = Service.SetReference(req, Service.GetHeaders(ActiveSession));

        if (txHdr.Nentries != 1)
        {
            throw new CorruptedDataException();
        }

        return TxHeader.ValueOf(txHdr);
    }

    public TxHeader SetReference(string key, string referencedKey, ulong atTx)
    {
        return SetReference(
            Utils.ToByteArray(key),
            Utils.ToByteArray(referencedKey),
            atTx);
    }

    public TxHeader SetReference(string key, string referencedKey)
    {
        return SetReference(
            Utils.ToByteArray(key),
            Utils.ToByteArray(referencedKey),
            0);
    }

    public TxHeader SetReference(byte[] key, byte[] referencedKey)
    {
        return SetReference(key, referencedKey, 0);
    }

    public TxHeader VerifiedSet(string key, byte[] value)
    {
        return VerifiedSet(Utils.ToByteArray(key), value);
    }

    public TxHeader VerifiedSet(string key, string value)
    {
        return VerifiedSet(Utils.ToByteArray(key), Utils.ToByteArray(value));
    }

    public TxHeader VerifiedSet(byte[] key, byte[] value)
    {
        CheckSessionHasBeenOpened();

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

        // using the ble VerifiableSetAsync is not ok here, because in the multithreading case it fails. Switched back to synchronous call in this case.

        var vtx = Service.VerifiableSet(vSetReq, Service.GetHeaders(ActiveSession));

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

        UpdateState(newState);
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

    public TxHeader ZAdd(byte[] set, byte[] key, ulong atTx, double score)
    {
        CheckSessionHasBeenOpened();
        ImmudbProxy.TxHeader txHdr = Service.ZAdd(
                new ImmudbProxy.ZAddRequest()
                {
                    Set = Utils.ToByteString(set),
                    Key = Utils.ToByteString(key),
                    AtTx = atTx,
                    Score = score,
                    BoundRef = atTx > 0
                }, Service.GetHeaders(ActiveSession));

        if (txHdr.Nentries != 1)
        {
            throw new CorruptedDataException();
        }

        return TxHeader.ValueOf(txHdr);
    }

    public TxHeader ZAdd(string set, string key, double score)
    {
        return ZAdd(Utils.ToByteArray(set), Utils.ToByteArray(key), score);
    }

    public TxHeader ZAdd(byte[] set, byte[] key, double score)
    {
        return ZAdd(set, key, 0, score);
    }

    public TxHeader VerifiedZAdd(byte[] set, byte[] key, ulong atTx, double score)
    {
        CheckSessionHasBeenOpened();

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

        ImmudbProxy.VerifiableTx vtx = Service.VerifiableZAdd(vZAddReq, Service.GetHeaders(ActiveSession));

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
        UpdateState(newState);
        return TxHeader.ValueOf(vtx.Tx.Header);
    }

    public TxHeader VerifiedZAdd(string set, string key, double score)
    {
        return VerifiedZAdd(Utils.ToByteArray(set), Utils.ToByteArray(key), score);
    }

    public TxHeader VerifiedZAdd(byte[] set, byte[] key, double score)
    {
        return VerifiedZAdd(set, key, 0, score);
    }

    public TxHeader VerifiedZAdd(string set, string key, ulong atTx, double score)
    {
        return VerifiedZAdd(Utils.ToByteArray(set), Utils.ToByteArray(key), atTx, score);
    }

    public List<ZEntry> ZScan(string set, ulong limit, bool reverse)
    {
        return ZScan(Utils.ToByteArray(set), limit, reverse);
    }

    public List<ZEntry> ZScan(byte[] set, ulong limit, bool reverse)
    {
        CheckSessionHasBeenOpened();
        ImmudbProxy.ZScanRequest req = new ImmudbProxy.ZScanRequest()
        {
            Set = Utils.ToByteString(set),
            Limit = limit,
            Desc = reverse
        };

        ImmudbProxy.ZEntries zEntries = Service.ZScan(req, Service.GetHeaders(ActiveSession));
        return BuildList(zEntries);
    }

    //
    // ========== DELETE ==========
    //

    public TxHeader Delete(string key)
    {
        return Delete(Utils.ToByteArray(key));
    }

    public TxHeader Delete(byte[] key)
    {
        CheckSessionHasBeenOpened();
        try
        {
            ImmudbProxy.DeleteKeysRequest req = new ImmudbProxy.DeleteKeysRequest()
            {
                Keys = { Utils.ToByteString(key) }
            };
            return TxHeader.ValueOf(Service.Delete(req, Service.GetHeaders(ActiveSession)));
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

    public Tx TxById(ulong txId)
    {
        CheckSessionHasBeenOpened();
        try
        {
            ImmudbProxy.Tx tx = Service.TxById(
                new ImmudbProxy.TxRequest()
                {
                    Tx = txId
                }, Service.GetHeaders(ActiveSession));
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

    public Tx VerifiedTxById(ulong txId)
    {
        CheckSessionHasBeenOpened();
        ImmuState state = State();
        ImmudbProxy.VerifiableTxRequest vTxReq = new ImmudbProxy.VerifiableTxRequest()
        {
            Tx = txId,
            ProveSinceTx = state.TxId
        };

        ImmudbProxy.VerifiableTx vtx;

        try
        {
            vtx = Service.VerifiableTxById(vTxReq, Service.GetHeaders(ActiveSession));
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
        UpdateState(newState);
        return tx;
    }

    private void UpdateState(ImmuState newState)
    {
        lock (stateSync)
        {
            stateHolder.SetState(ActiveSession!, newState);
        }
    }

    public List<Tx> TxScan(ulong initialTxId)
    {
        CheckSessionHasBeenOpened();
        ImmudbProxy.TxScanRequest req = new ImmudbProxy.TxScanRequest()
        {
            InitialTx = initialTxId
        };

        ImmudbProxy.TxList txList = Service.TxScan(req, Service.GetHeaders(ActiveSession));
        return buildList(txList);
    }

    public List<Tx> TxScan(ulong initialTxId, uint limit, bool desc)
    {
        ImmudbProxy.TxScanRequest req = new ImmudbProxy.TxScanRequest()
        {
            InitialTx = initialTxId,
            Limit = limit,
            Desc = desc
        };
        ImmudbProxy.TxList txList = Service.TxScan(req, Service.GetHeaders(ActiveSession));
        return buildList(txList);
    }

    //
    // ========== HEALTH ==========
    //

    public bool HealthCheck()
    {
        var healthResponse = Service.Health(new Empty(), Service.GetHeaders(ActiveSession));
        return healthResponse.Status;
    }

    public bool IsConnected()
    {
        return !Connection.Released;
    }

    //
    // ========== USER MGMT ==========
    //

    public List<Iam.User> ListUsers()
    {
        CheckSessionHasBeenOpened();
        ImmudbProxy.UserList userList = Service.ListUsers(new Empty(), Service.GetHeaders(ActiveSession));
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

    public void CreateUser(string user, string password, Iam.Permission permission, string database)
    {
        CheckSessionHasBeenOpened();
        ImmudbProxy.CreateUserRequest createUserRequest = new ImmudbProxy.CreateUserRequest()
        {
            User = Utils.ToByteString(user),
            Password = Utils.ToByteString(password),
            Permission = (uint)permission,
            Database = database
        };

        Service.CreateUser(createUserRequest, Service.GetHeaders(ActiveSession));
    }

    public void ChangePassword(string user, string oldPassword, string newPassword)
    {
        CheckSessionHasBeenOpened();
        ImmudbProxy.ChangePasswordRequest changePasswordRequest = new ImmudbProxy.ChangePasswordRequest()
        {
            User = Utils.ToByteString(user),
            OldPassword = Utils.ToByteString(oldPassword),
            NewPassword = Utils.ToByteString(newPassword),
        };

        Service.ChangePassword(changePasswordRequest, Service.GetHeaders(ActiveSession));
    }

    //
    // ========== INDEX MGMT ==========
    //

    public void FlushIndex(float cleanupPercentage, bool synced)
    {
        CheckSessionHasBeenOpened();
        ImmudbProxy.FlushIndexRequest req = new ImmudbProxy.FlushIndexRequest()
        {
            CleanupPercentage = cleanupPercentage,
            Synced = synced
        };

        Service.FlushIndex(req, Service.GetHeaders(ActiveSession));
    }

    public void CompactIndex()
    {
        CheckSessionHasBeenOpened();
        Service.CompactIndex(new Empty(), Service.GetHeaders(ActiveSession));
    }

    //
    // ========== HISTORY ==========
    //

    public List<Entry> History(string key, int limit, ulong offset, bool desc)
    {
        return History(Utils.ToByteArray(key), limit, offset, desc);
    }

    public List<Entry> History(byte[] key, int limit, ulong offset, bool desc)
    {
        CheckSessionHasBeenOpened();
        try
        {
            ImmudbProxy.Entries entries = Service.History(new ImmudbProxy.HistoryRequest()
            {
                Key = Utils.ToByteString(key),
                Limit = limit,
                Offset = offset,
                Desc = desc
            }, Service.GetHeaders(ActiveSession));

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
                Console.WriteLine("An exception occurred into buildList: {0}", e);
            }
        });
        return result;
    }
}