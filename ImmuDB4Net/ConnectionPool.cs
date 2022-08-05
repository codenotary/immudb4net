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

using static ImmuDB.ImmuClient;

public interface IConnectionPool
{
    IConnection Acquire(ImmuClient client);
    void Release(ImmuClient client);
    Task Shutdown();
}

public class TrivialConnectionPool : IConnectionPool
{
    const int MAX_CONNECTIONS = 1;
    private ImmuClientBuilder builder;
    private Random random = new Random(Environment.TickCount);
    private ReleasedConnection releasedConnection;

    public TrivialConnectionPool(ImmuClientBuilder builder)
    {
        this.builder = builder;
        this.releasedConnection = new ReleasedConnection(this);
    }

    List<IConnection> connections = new List<IConnection>();
    Dictionary<ImmuClient, IConnection> _assignments = new Dictionary<ImmuClient, IConnection>();

    public IConnection Acquire(ImmuClient client)
    {
        if (connections.Count < MAX_CONNECTIONS)
        {
            var conn = new Connection(builder);
            connections.Add(conn);
            _assignments[client] = conn;
            return conn;
        }
        var randomConn = connections[random.Next(MAX_CONNECTIONS)];
        _assignments[client] = randomConn;
        return randomConn;
    }

    public void Release(ImmuClient client)
    {
        _assignments.Remove(client);
        client.Connection = releasedConnection;
    }

    public async Task Shutdown()
    {
        foreach(var connection in connections) {
            await connection.Shutdown();
        }
    }
}