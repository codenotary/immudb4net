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
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using ImmuDB.Crypto;
using ImmuDB.Exceptions;
using ImmudbProxy;
using Org.BouncyCastle.Crypto;

public class ImmuClient
{
    internal const string AUTH_HEADER = "authorization";

    private readonly AsymmetricKeyParameter? serverSigningKey;
    private readonly ImmuStateHolder stateHolder;
    private GrpcChannel? channel;

    public string CurrentServerUuid { get; set; } = "";

    private string currentDb = "defaultdb";
    internal ImmuService.ImmuServiceClient Service { get; private set; }


    public static Builder NewBuilder()
    {
        return new Builder();
    }

    public ImmuClient(Builder builder)
    {
        string schema = builder.ServerUrl.StartsWith("http") ? "" : "http://";
        var grpcAddress = $"{schema}{builder.ServerUrl}:{builder.ServerPort}";

        // This is needed for .NET Core 3 and below.
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        channel = GrpcChannel.ForAddress(grpcAddress);
        var invoker = channel.Intercept(new ImmuServerUUIDInterceptor(this));
        Service = new ImmuService.ImmuServiceClient(invoker);
        Service.WithAuth = builder.Auth;
        serverSigningKey = builder.ServerSigningKey;
        stateHolder = builder.StateHolder;
    }

    public async Task Shutdown()
    {
        if (channel == null)
        {
            return;
        }
        Task shutdownTask = channel.ShutdownAsync();
        await Task.WhenAny(shutdownTask, Task.Delay(TimeSpan.FromSeconds(2)));
        channel = null;
    }

    public bool IsShutdown()
    {
        lock (this)
        {
            return channel == null;
        }
    }

    public async Task Login(string username, string password)
    {
        ImmudbProxy.LoginRequest loginRequest = new ImmudbProxy.LoginRequest()
        {
            User = Utils.ToByteString(username),
            Password = Utils.ToByteString(password)
        };
        ImmudbProxy.LoginResponse loginResponse = await Service.LoginAsync(loginRequest);
        Service.AuthToken = loginResponse.Token;
    }

    public async Task Logout()
    {
        await Service.WithAuthHeaders().LogoutAsync(new Empty(), Service.Headers);
    }

    public async Task<ImmuState> State()
    {
        ImmuState? state = stateHolder.GetState(CurrentServerUuid, currentDb);
        if (state == null)
        {
            state = await CurrentState();
            stateHolder.SetState(CurrentServerUuid, state);
        }
        return state;
    }

    /**
    * Get the current database state that exists on the server.
    * It may throw a RuntimeException if server's state signature verification fails
    * (if this feature is enabled on the client side, at least).
    */
    public async Task<ImmuState> CurrentState()
    {
        ImmudbProxy.ImmutableState state = await Service.WithAuthHeaders().CurrentStateAsync(new Empty(), Service.Headers);
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
        ImmudbProxy.CreateDatabaseRequest db = new ImmudbProxy.CreateDatabaseRequest
        {
            Name = database
        };

        await Service.WithAuthHeaders().CreateDatabaseV2Async(db, Service.Headers);
    }

    public async Task UseDatabase(string database)
    {
        ImmudbProxy.Database db = new ImmudbProxy.Database
        {
            DatabaseName = database
        };
        ImmudbProxy.UseDatabaseReply response = await Service.WithAuthHeaders().UseDatabaseAsync(db, Service.Headers);
        Service.AuthToken = response.Token;
        currentDb = database;
    }

    public async Task<List<string>> Databases()
    {
        ImmudbProxy.DatabaseListRequestV2 req = new ImmudbProxy.DatabaseListRequestV2();
        ImmudbProxy.DatabaseListResponseV2 res = await Service.WithAuthHeaders().DatabaseListV2Async(req, Service.Headers);
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
        ImmudbProxy.KeyRequest req = new ImmudbProxy.KeyRequest
        {
            Key = Utils.ToByteString(key),
            AtTx = tx
        };
        try
        {
            ImmudbProxy.Entry entry = await Service.WithAuthHeaders().GetAsync(req, Service.Headers);
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
        ImmudbProxy.VerifiableGetRequest vGetReq = new ImmudbProxy.VerifiableGetRequest()
        {
            KeyRequest = keyReq,
            ProveSinceTx = state.TxId
        };

        ImmudbProxy.VerifiableEntry vEntry;

        try
        {
            vEntry = await Service.WithAuthHeaders().VerifiableGetAsync(vGetReq, Service.Headers);
        }
        catch (RpcException e)
        {
            if (e.Message.Contains("key not found"))
            {
                throw new KeyNotFoundException();
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

        stateHolder.SetState(CurrentServerUuid, newState);

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

        return await VerifiedGet(keyReq, await State());
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

        return await VerifiedGet(keyReq, await State());
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

        return await VerifiedGet(keyReq, await State());
    }

    public async Task<Entry> GetSinceTx(string key, ulong tx)
    {
        return await GetSinceTx(Utils.ToByteArray(key), tx);
    }

    public async Task<Entry> GetSinceTx(byte[] key, ulong tx)
    {
        ImmudbProxy.KeyRequest req = new ImmudbProxy.KeyRequest()
        {
            Key = Utils.ToByteString(key),
            SinceTx = tx
        };

        try
        {
            return Entry.ValueOf(await Service.WithAuthHeaders().GetAsync(req, Service.Headers));
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
        ImmudbProxy.KeyRequest req = new ImmudbProxy.KeyRequest()
        {
            Key = Utils.ToByteString(key),
            AtRevision = rev
        };

        try
        {
            return Entry.ValueOf(await Service.WithAuthHeaders().GetAsync(req, Service.Headers));
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

    public async Task<List<Entry>> GetAll(List<String> keys)
    {
        List<ByteString> keysBS = new List<ByteString>(keys.Count);

        foreach (string key in keys)
        {
            keysBS.Add(Utils.ToByteString(key));
        }

        ImmudbProxy.KeyListRequest req = new ImmudbProxy.KeyListRequest();
        req.Keys.AddRange(keysBS);
        
        ImmudbProxy.Entries entries = await Service.WithAuthHeaders().GetAllAsync(req, Service.Headers);
        List<Entry> result = new List<Entry>(entries.Entries_.Count);

        foreach (ImmudbProxy.Entry entry in entries.Entries_)
        {
            result.Add(Entry.ValueOf(entry));
        }

        return result;
    }

    //
    // ========== SET ==========
    //

    public async Task<TxHeader> Set(byte[] key, byte[] value)
    {
        ImmudbProxy.KeyValue kv = new ImmudbProxy.KeyValue()
        {
            Key = Utils.ToByteString(key),
            Value = Utils.ToByteString(value)
        };

        ImmudbProxy.SetRequest req = new ImmudbProxy.SetRequest();
        req.KVs.Add(kv);

        ImmudbProxy.TxHeader txHdr = await Service.WithAuthHeaders().SetAsync(req, Service.Headers);

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

    public async Task<TxHeader> SetAll(List<KVPair> kvList)
    {
        ImmudbProxy.SetRequest request = new ImmudbProxy.SetRequest();

        foreach (KVPair kv in kvList)
        {
            ImmudbProxy.KeyValue kvProxy = new ImmudbProxy.KeyValue();

            kvProxy.Key = Utils.ToByteString(kv.Key);
            kvProxy.Value = Utils.ToByteString(kv.Value);
            request.KVs.Add(kvProxy);
        }

        ImmudbProxy.TxHeader txHdr = await Service.WithAuthHeaders().SetAsync(request, Service.Headers);

        if (txHdr.Nentries != kvList.Count)
        {
            throw new CorruptedDataException();
        }

        return TxHeader.ValueOf(txHdr);
    }

    public async Task<TxHeader> SetReference(byte[] key, byte[] referencedKey, ulong atTx)
    {
        ImmudbProxy.ReferenceRequest req = new ImmudbProxy.ReferenceRequest()
        {
            Key = Utils.ToByteString(key),
            ReferencedKey = Utils.ToByteString(referencedKey),
            AtTx = atTx,
            BoundRef = atTx > 0
        };

        ImmudbProxy.TxHeader txHdr = await Service.WithAuthHeaders().SetReferenceAsync(req, Service.Headers);

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

    public async Task<TxHeader> VerifiedSet(string key, byte[] value)
    {
        return await VerifiedSet(Utils.ToByteArray(key), value);
    }

    public async Task<TxHeader> VerifiedSet(byte[] key, byte[] value)
    {
        ImmuState state = await State();
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

        ImmudbProxy.VerifiableTx vtx = await Service.WithAuthHeaders().VerifiableSetAsync(vSetReq, Service.Headers);
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

        stateHolder.SetState(CurrentServerUuid, newState);

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
        ImmudbProxy.TxHeader txHdr = await Service.WithAuthHeaders().ZAddAsync(
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
        ImmuState state = await State();

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

        ImmudbProxy.VerifiableTx vtx = await Service.WithAuthHeaders().VerifiableZAddAsync(vZAddReq, Service.Headers);

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

        stateHolder.SetState(CurrentServerUuid, newState);

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
        ImmudbProxy.ZScanRequest req = new ImmudbProxy.ZScanRequest()
        {
            Set = Utils.ToByteString(set),
            Limit = limit,
            Desc = reverse
        };

        ImmudbProxy.ZEntries zEntries = await Service.WithAuthHeaders().ZScanAsync(req, Service.Headers);

        return buildList(zEntries);
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
        try
        {
            ImmudbProxy.DeleteKeysRequest req = new ImmudbProxy.DeleteKeysRequest();
            req.Keys.Add(Utils.ToByteString(key));

            return TxHeader.ValueOf(await Service.WithAuthHeaders().DeleteAsync(req, Service.Headers));
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
        try
        {
            ImmudbProxy.Tx tx = await Service.WithAuthHeaders().TxByIdAsync(
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
        ImmuState state = await State();
        ImmudbProxy.VerifiableTxRequest vTxReq = new ImmudbProxy.VerifiableTxRequest()
        {
            Tx = txId,
            ProveSinceTx = state.TxId
        };

        ImmudbProxy.VerifiableTx vtx;

        try
        {
            vtx = await Service.WithAuthHeaders().VerifiableTxByIdAsync(vTxReq, Service.Headers);
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

        stateHolder.SetState(CurrentServerUuid, newState);

        return tx;
    }

    public async Task<List<Tx>> TxScan(ulong initialTxId)
    {
        ImmudbProxy.TxScanRequest req = new ImmudbProxy.TxScanRequest()
        {
            InitialTx = initialTxId
        };

        ImmudbProxy.TxList txList = await Service.WithAuthHeaders().TxScanAsync(req, Service.Headers);
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
        ImmudbProxy.TxList txList = await Service.WithAuthHeaders().TxScanAsync(req, Service.Headers);
        return buildList(txList);
    }

    //
    // ========== HEALTH ==========
    //

    public async Task<bool> HealthCheck()
    {
        var healthResponse = await Service.WithAuthHeaders().HealthAsync(new Empty(), Service.Headers);
        return healthResponse.Status;
    }

    public bool isConnected()
    {
        return channel != null;
    }

    //
    // ========== USER MGMT ==========
    //

    public async Task<List<Iam.User>> ListUsers()
    {
        ImmudbProxy.UserList userList = await Service.WithAuthHeaders().ListUsersAsync(new Empty(), Service.Headers);

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
        ImmudbProxy.CreateUserRequest createUserRequest = new ImmudbProxy.CreateUserRequest()
        {
            User = Utils.ToByteString(user),
            Password = Utils.ToByteString(password),
            Permission = (uint)permission,
            Database = database
        };

        await Service.WithAuthHeaders().CreateUserAsync(createUserRequest, Service.Headers);
    }

    public async Task ChangePassword(string user, string oldPassword, string newPassword)
    {
        ImmudbProxy.ChangePasswordRequest changePasswordRequest = new ImmudbProxy.ChangePasswordRequest()
        {
            User = Utils.ToByteString(user),
            OldPassword = Utils.ToByteString(oldPassword),
            NewPassword = Utils.ToByteString(newPassword),
        };

        await Service.WithAuthHeaders().ChangePasswordAsync(changePasswordRequest, Service.Headers);
    }

    //
    // ========== INDEX MGMT ==========
    //

    public async Task FlushIndex(float cleanupPercentage, bool synced)
    {
        ImmudbProxy.FlushIndexRequest req = new ImmudbProxy.FlushIndexRequest()
        {
            CleanupPercentage = cleanupPercentage,
            Synced = synced
        };

        await Service.WithAuthHeaders().FlushIndexAsync(req, Service.Headers);
    }

    public async Task CompactIndex()
    {
        await Service.WithAuthHeaders().CompactIndexAsync(new Empty(), Service.Headers);
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
        try
        {
            ImmudbProxy.Entries entries = await Service.WithAuthHeaders().HistoryAsync(new ImmudbProxy.HistoryRequest()
            {
                Key = Utils.ToByteString(key),
                Limit = limit,
                Offset = offset,
                Desc = desc
            }, Service.Headers);

            return buildList(entries);
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

    private List<Entry> buildList(ImmudbProxy.Entries entries)
    {
        List<Entry> result = new List<Entry>(entries.Entries_.Count);
        entries.Entries_.ToList().ForEach(entry => result.Add(Entry.ValueOf(entry)));
        return result;
    }

    private List<ZEntry> buildList(ImmudbProxy.ZEntries entries)
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

    ///Builder is an inner class that implements the builder pattern for ImmuClient
    public class Builder
    {
        public string ServerUrl { get; private set; }
        public int ServerPort { get; private set; }
        public AsymmetricKeyParameter? ServerSigningKey { get; private set; }
        public bool Auth { get; private set; }
        public ImmuStateHolder StateHolder { get; private set; }


        public Builder()
        {
            ServerUrl = "localhost";
            ServerPort = 3322;
            StateHolder = new SerializableImmuStateHolder();
            Auth = true;
        }

        public Builder WithStateHolder(ImmuStateHolder stateHolder)
        {
            StateHolder = stateHolder;
            return this;
        }

        public Builder WithAuth(bool withAuth)
        {
            this.Auth = withAuth;
            return this;
        }

        public Builder WithServerPort(int serverPort)
        {
            this.ServerPort = serverPort;
            return this;
        }

        public Builder WithServerUrl(string serverUrl)
        {
            this.ServerUrl = serverUrl;
            return this;
        }

        public Builder WithServerSigningKey(string publicKeyFileName)
        {
            this.ServerSigningKey = ImmuState.GetPublicKeyFromPemFile(publicKeyFileName);
            return this;
        }

        public ImmuClient Build()
        {
            return new ImmuClient(this);
        }
    }
}