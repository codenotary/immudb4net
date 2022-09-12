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
    int MaxConnectionsPerServer { get; }
    IConnection Acquire(ConnectionParameters cp);
    Task Release(IConnection con);
    Task Shutdown();
}



public class RandomAssignConnectionPool : IConnectionPool
{
    public int MaxConnectionsPerServer { get; private set; }
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

    internal class Item
    {
        public Item(IConnection con)
        {
            Connection = con;
            RefCount = 0;
        }
        public IConnection Connection { get; set; }
        public int RefCount { get; set; }
    }

    internal RandomAssignConnectionPool()
    {
        MaxConnectionsPerServer = ImmuClient.GlobalSettings.MaxConnectionsPerServer;
        this.releasedConnection = new ReleasedConnection();
    }

    Dictionary<string, List<Item>> connections = new Dictionary<string, List<Item>>();

    public IConnection Acquire(ConnectionParameters cp)
    {
        lock (this)
        {
            List<Item> poolForAddress;
            if (!connections.TryGetValue(cp.Address, out poolForAddress))
            {
                poolForAddress = new List<Item>();
                var conn = new Connection(cp);
                var item = new Item(conn);
                poolForAddress.Add(item);
                connections.Add(cp.Address, poolForAddress);
                return conn;
            }
            if (poolForAddress.Count < MaxConnectionsPerServer)
            {
                var conn = new Connection(cp);
                var item = new Item(conn);
                poolForAddress.Add(item);
                return conn;
            }
            var randomItem = poolForAddress[random.Next(MaxConnectionsPerServer)];
            randomItem.RefCount++;
            return randomItem.Connection;
        }
    }

    ///<summary>
    /// The method <c>Release</c> is called when the connection specified as argument is no more used by an <c>ImmuClient</c> instance, therefore it
    /// can be closed 
    ///</summary>
    public async Task Release(IConnection con)
    {
        List<Item> poolForAddress;
        int indexToRemove = -1;
        lock (this)
        {
            if (!connections.TryGetValue(con.Address, out poolForAddress)) 
            {
                return;
            }
            
            for(int i = 0; i < poolForAddress.Count; i++)
            {
                if((poolForAddress[i].Connection == con) && (poolForAddress[i].RefCount > 0))
                {
                    poolForAddress[i].RefCount--;
                    if(poolForAddress[i].RefCount == 0) 
                    {
                        indexToRemove = i;
                    }
                    break;
                }
            }
            if(indexToRemove > -1)
            {
                poolForAddress.RemoveAt(indexToRemove);
            }
        }
        if(indexToRemove > -1)
        {
            await con.Shutdown();
        }
    }

    public async Task Shutdown()
    {
        Dictionary<string, List<Item>> clone = new Dictionary<string, List<Item>>();
        lock (this)
        {
            foreach (var addressPool in connections)
            {
                List<Item> connections = new List<Item>(addressPool.Value);
                clone.Add(addressPool.Key, connections);
            }
        }
        foreach (var addressPool in clone)
        {
            foreach (var item in addressPool.Value)
            {
                await item.Connection.Shutdown();
            }
        }
    }
}