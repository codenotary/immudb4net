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


/// <summary>
/// Enables the creation of <see cref="ImmuClientSync" /> instances
/// </summary>
public class ImmuClientSyncBuilder
{
    public string ServerUrl { get; private set; }
    public string Username { get; private set; }
    public string Password { get; private set; }
    public string Database { get; private set; }
    public int ServerPort { get; private set; }
    public AsymmetricKeyParameter? ServerSigningKey { get; private set; }
    public bool DeploymentInfoCheck { get; private set; }
    public IImmuStateHolder StateHolder { get; private set; }
    public TimeSpan HeartbeatInterval { get; set; }

    internal IConnectionPool ConnectionPool { get; }
    internal ISessionManager SessionManager { get; }

    static ImmuClientSyncBuilder()
    {
        // This is needed for .NET Core 3 and below.
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
    }

    /// <summary>
    /// The constructor for ImmuClientSyncBuilder, sets the default values for the fields
    /// </summary>
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

    /// <summary>
    /// Gets the GrpcAddress; it is formed from the ServerUrl and ServerPort parameters
    /// </summary>
    /// <value></value>
    public string GrpcAddress
    {
        get
        {
            string schema = ServerUrl.StartsWith("http") ? "" : "http://";
            return $"{schema}{ServerUrl.ToLowerInvariant()}:{ServerPort}";
        }
    }

    /// <summary>
    /// Gets the length of time the <see cref="ImmuClient.Shutdown" /> function is allowed to block before it completes.
    /// </summary>
    /// <value>Default: 2 sec</value>
    public TimeSpan ConnectionShutdownTimeout { get; internal set; }

    /// <summary>
    /// Sets a stateholder instance. It could be a custom state holder that implements IImmuStateHolder
    /// </summary>
    /// <param name="stateHolder"></param>
    /// <returns></returns>
    public ImmuClientSyncBuilder WithStateHolder(IImmuStateHolder stateHolder)
    {
        StateHolder = stateHolder;
        return this;
    }

    /// <summary>
    /// Sets the CheckDeploymentInfo flag. If this flag is set then a check of server authenticity is perform while establishing a new link with the ImmuDB server.
    /// </summary>
    /// <param name="check"></param>
    /// <returns></returns>
    public ImmuClientSyncBuilder CheckDeploymentInfo(bool check)
    {
        this.DeploymentInfoCheck = check;
        return this;
    }

    /// <summary>
    /// Sets the credentials
    /// </summary>
    /// <param name="username">The username</param>
    /// <param name="password">The password</param>
    /// <returns></returns>
    public ImmuClientSyncBuilder WithCredentials(string username, string password)
    {
        this.Username = username;
        this.Password = password;
        return this;
    }

    /// <summary>
    /// Sets the database name
    /// </summary>
    /// <param name="databaseName"></param>
    /// <returns></returns>
    public ImmuClientSyncBuilder WithDatabase(string databaseName)
    {
        this.Database = databaseName;
        return this;
    }

    /// <summary>
    /// Sets the port number where the ImmuDB listens to
    /// </summary>
    /// <param name="serverPort"></param>
    /// <returns></returns>
    public ImmuClientSyncBuilder WithServerPort(int serverPort)
    {
        this.ServerPort = serverPort;
        return this;
    }

    /// <summary>
    /// Sets the server URL 
    /// </summary>
    /// <param name="serverUrl"></param>
    /// <returns></returns>
    public ImmuClientSyncBuilder WithServerUrl(string serverUrl)
    {
        this.ServerUrl = serverUrl;
        return this;
    }

    /// <summary>
    /// Sets the time interval between heartbeat gRPC calls
    /// </summary>
    /// <param name="heartbeatInterval"></param>
    /// <returns></returns>
    public ImmuClientSyncBuilder WithHeartbeatInterval(TimeSpan heartbeatInterval)
    {
        this.HeartbeatInterval = heartbeatInterval;
        return this;
    }

    /// <summary>
    /// Sets the length of time the <see cref="ImmuClientSync.Shutdown" /> function is allowed to block before it completes.
    /// </summary>
    /// <param name="timeout"></param>
    /// <returns></returns>
    public ImmuClientSyncBuilder WithConnectionShutdownTimeout(TimeSpan timeout)
    {
        this.ConnectionShutdownTimeout = timeout;
        return this;
    }

    /// <summary>
    /// Sets the file path containing the signing public key of ImmuDB server
    /// </summary>
    /// <param name="publicKeyFileName"></param>
    /// <returns></returns>
    public ImmuClientSyncBuilder WithServerSigningKey(string publicKeyFileName)
    {
        this.ServerSigningKey = ImmuState.GetPublicKeyFromPemFile(publicKeyFileName);
        return this;
    }

    /// <summary>
    /// Sets the server signing key
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public ImmuClientSyncBuilder WithServerSigningKey(AsymmetricKeyParameter? key)
    {
        this.ServerSigningKey = key;
        return this;
    }

    /// <summary>
    /// Creates an <see cref="ImmuClientSync" /> instance using the parameters defined in the builder. One can use the builder's fluent interface to define these parameters.
    /// </summary>
    /// <returns></returns>
    public ImmuClientSync Build()
    {
        return new ImmuClientSync(this);
    }

    /// <summary>
    /// Creates an <see cref="ImmuClientSync" /> instance using the parameters from the builder instance and opens a connection to the server.
    /// </summary>
    /// <returns></returns>
    public ImmuClientSync Open()
    {
        var immuClient = new ImmuClientSync(this);
        immuClient.Open(this.Username, this.Password, this.Database);
        return immuClient;
    }
}
