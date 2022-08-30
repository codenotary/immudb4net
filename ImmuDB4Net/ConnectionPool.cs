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


public interface IConnectionPool
{
    int MaxConnectionsPerServer { get; set; }
    IConnection Acquire(ImmuClient client);
    void Release(ImmuClient client);
    Task Shutdown();
}

public class RandomAssignConnectionPool : IConnectionPool
{
    public int MaxConnectionsPerServer { get; set; }
    private Random random = new Random(Environment.TickCount);
    private ReleasedConnection releasedConnection;

    internal static RandomAssignConnectionPool _instance = new RandomAssignConnectionPool();
    public static IConnectionPool Instance
    {
        get
        {
            return _instance;
        }
    }

    internal RandomAssignConnectionPool()
    {
        MaxConnectionsPerServer = ImmuClient.GlobalSettings.MaxConnectionsPerServer;
        this.releasedConnection = new ReleasedConnection(this);
    }

    Dictionary<string, List<IConnection>> connections = new Dictionary<string, List<IConnection>>();
    Dictionary<ImmuClient, IConnection> _assignments = new Dictionary<ImmuClient, IConnection>();

    public IConnection Acquire(ImmuClient client)
    {
        lock (this)
        {
            List<IConnection> poolForAddress;
            if (!connections.TryGetValue(client.GrpcAddress, out poolForAddress))
            {
                poolForAddress = new List<IConnection>();
                var conn = new Connection(client);
                poolForAddress.Add(conn);
                connections.Add(client.GrpcAddress, poolForAddress);
                _assignments[client] = conn;
                return conn;
            }
            if (poolForAddress.Count < MaxConnectionsPerServer)
            {
                var conn = new Connection(client);
                poolForAddress.Add(conn);
                return conn;
            }
            var randomConn = poolForAddress[random.Next(MaxConnectionsPerServer)];
            _assignments[client] = randomConn;
            return randomConn;
        }
    }

    public void Release(ImmuClient client)
    {
        lock (this)
        {
            _assignments.Remove(client);
            client.Connection = releasedConnection;
        }
    }

    public async Task Shutdown()
    {
        Dictionary<string, List<IConnection>> clone = new Dictionary<string, List<IConnection>>();
        lock (this)
        {
            foreach (var addressPool in connections)
            {
                List<IConnection> connections = new List<IConnection>(addressPool.Value);
                clone.Add(addressPool.Key, connections);
            }
        }
        foreach (var addressPool in clone)
        {
            foreach (var connection in addressPool.Value)
            {
                await connection.Shutdown();
            }
        }
    }
}