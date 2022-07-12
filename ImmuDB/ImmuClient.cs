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

using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using ImmuDB.Exceptions;
using ImmudbProxy;
using Org.BouncyCastle.Crypto;

public class ImmuClient
{
    internal const string AUTH_HEADER = "authorization";

    private readonly AsymmetricKeyParameter? serverSigningKey;
    private readonly ImmuStateHolder stateHolder;
    private GrpcChannel? channel;

    public string? CurrentServerUuid { get; set; }

    private string currentDb = "defaultdb";
    internal ImmuService.ImmuServiceClient immuServiceClient;
    internal Metadata headers = new Metadata();

    public static Builder NewBuilder()
    {
        return new Builder();
    }

    public ImmuClient(Builder builder)
    {
        string schema = builder.ServerUrl.StartsWith("http") ? "" : "http://";
        var grpcAddress = $"{schema}{builder.ServerUrl}:${builder.ServerPort}";
        channel = GrpcChannel.ForAddress(grpcAddress);
        var invoker = channel.Intercept(new ImmuServerUUIDInterceptor(this));
        immuServiceClient = new ImmuService.ImmuServiceClient(invoker);
        this.immuServiceClient.WithAuth = builder.Auth;
        this.serverSigningKey = builder.ServerSigningKey;
        this.stateHolder = builder.StateHolder;
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
        ImmudbProxy.LoginResponse loginResponse = await immuServiceClient.LoginAsync(loginRequest);
        immuServiceClient.AuthToken = loginResponse.Token;
    }

    public async Task Logout()
    {
        await immuServiceClient.WithAuthHeaders().LogoutAsync(new Empty(), this.headers);
    }

    public async Task<ImmuState?> State()
    {
        ImmuState? state = stateHolder.GetState(CurrentServerUuid, currentDb);
        if (state == null)
        {
            state = await CurrentState();
            stateHolder.setState(CurrentServerUuid, state);
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
        ImmudbProxy.ImmutableState state = await immuServiceClient.WithAuthHeaders().CurrentStateAsync(new Empty(), this.headers);
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

    public async Task CreateDatabase(String database)
    {
        ImmudbProxy.CreateDatabaseRequest db = new ImmudbProxy.CreateDatabaseRequest
        {
            Name = database
        };

        await immuServiceClient.WithAuthHeaders().CreateDatabaseV2Async(db);
    }

    public async Task UseDatabase(String database)
    {
        ImmudbProxy.Database db = new ImmudbProxy.Database
        {
            DatabaseName = database
        };
        ImmudbProxy.UseDatabaseReply response = await immuServiceClient.WithAuthHeaders().UseDatabaseAsync(db);
        immuServiceClient.AuthToken = response.Token;
        currentDb = database;
    }

    public async Task<List<String>> Databases()
    {
        ImmudbProxy.DatabaseListRequestV2 req = new ImmudbProxy.DatabaseListRequestV2();
        ImmudbProxy.DatabaseListResponseV2 res = await immuServiceClient.WithAuthHeaders().DatabaseListV2Async(req);
        List<String> list = new List<String>(res.Databases.Count);
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
            ImmudbProxy.Entry entry = await immuServiceClient.WithAuthHeaders().GetAsync(req);
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

    public async Task<Entry> Get(String key, ulong tx)
    {
        return await GetAtTx(Utils.ToByteArray(key), tx);
    }

    public async Task<Entry> Get(String key)
    {
        return await GetAtTx(Utils.ToByteArray(key), 0);
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

        ImmudbProxy.TxHeader txHdr = await immuServiceClient.WithAuthHeaders().SetAsync(req);

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
    }
}