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
using Org.BouncyCastle.Crypto;


///Builder is an inner class that implements the builder pattern for ImmuClient
public class ImmuClientSyncBuilder
{
    public string ServerUrl { get; private set; }
    public string Username { get; private set; }
    public string Password { get; private set; }
    public string Database { get; private set; }
    public int ServerPort { get; private set; }
    public AsymmetricKeyParameter? ServerSigningKey { get; private set; }
    public bool DeploymentInfoCheck { get; private set; }
    public ImmuStateHolder StateHolder { get; private set; }
    public TimeSpan HeartbeatInterval { get; set; }

    internal IConnectionPool ConnectionPool { get; }
    internal ISessionManager SessionManager { get; }

    static ImmuClientSyncBuilder()
    {
        // This is needed for .NET Core 3 and below.
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    public ImmuClientSyncBuilder()
    {
        ServerUrl = "localhost";
        ServerPort = 3322;
        Username = "immudb";
        Password = "immudb";
        Database = "defaultdb";
        StateHolder = new FileImmuStateHolder();
        DeploymentInfoCheck = true;
        HeartbeatInterval = TimeSpan.FromMinutes(1);
        ConnectionPool = RandomAssignConnectionPool.Instance;
        SessionManager = DefaultSessionManager.Instance;
        ConnectionShutdownTimeout = TimeSpan.FromSeconds(2);
    }

    public string GrpcAddress
    {
        get
        {
            string schema = ServerUrl.StartsWith("http") ? "" : "http://";
            return $"{schema}{ServerUrl.ToLowerInvariant()}:{ServerPort}";
        }
    }

    public TimeSpan ConnectionShutdownTimeout { get; internal set; }

    public ImmuClientSyncBuilder WithStateHolder(ImmuStateHolder stateHolder)
    {
        StateHolder = stateHolder;
        return this;
    }

    public ImmuClientSyncBuilder CheckDeploymentInfo(bool check)
    {
        this.DeploymentInfoCheck = check;
        return this;
    }

    public ImmuClientSyncBuilder WithCredentials(string username, string password)
    {
        this.Username = username;
        this.Password = password;
        return this;
    }

    public ImmuClientSyncBuilder WithDatabase(string databaseName)
    {
        this.Database = databaseName;
        return this;
    }

    public ImmuClientSyncBuilder WithServerPort(int serverPort)
    {
        this.ServerPort = serverPort;
        return this;
    }

    public ImmuClientSyncBuilder WithServerUrl(string serverUrl)
    {
        this.ServerUrl = serverUrl;
        return this;
    }

    public ImmuClientSyncBuilder WithHeartbeatInterval(TimeSpan heartbeatInterval)
    {
        this.HeartbeatInterval = heartbeatInterval;
        return this;
    }

    public ImmuClientSyncBuilder WithConnectionShutdownTimeout(TimeSpan timeout)
    {
        this.ConnectionShutdownTimeout = timeout;
        return this;
    }

    public ImmuClientSyncBuilder WithServerSigningKey(string publicKeyFileName)
    {
        this.ServerSigningKey = ImmuState.GetPublicKeyFromPemFile(publicKeyFileName);
        return this;
    }

    public ImmuClientSyncBuilder WithServerSigningKey(AsymmetricKeyParameter? key)
    {
        this.ServerSigningKey = key;
        return this;
    }

    public ImmuClientSync Build()
    {
        return new ImmuClientSync(this);
    }

    public ImmuClientSync Open()
    {
        var immuClient = new ImmuClientSync(this);
        immuClient.Open(this.Username, this.Password, this.Database);
        return immuClient;
    }
}
