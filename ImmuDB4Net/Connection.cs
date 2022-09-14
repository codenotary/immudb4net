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

using Grpc.Net.Client;
using ImmudbProxy;

namespace ImmuDB;

public interface IConnection
{
    string Address { get; }
    ImmuService.ImmuServiceClient Service { get; }
    Task ShutdownAsync();
    void Shutdown();
    bool Released { get; }
}

public class ConnectionParameters
{
    public string Address { get; set; } = "";
    public TimeSpan ShutdownTimeout { get; set; }
}

public class Connection : IConnection
{
    private ImmuService.ImmuServiceClient grpcClient;
    public ImmuService.ImmuServiceClient Service => grpcClient;
    private GrpcChannel? channel;
    public string Address { get; private set; }
    public TimeSpan shutdownTimeout;
    public bool Released => channel == null;

    internal Connection(ConnectionParameters parameters)
    {
        Address = parameters.Address;
        channel = GrpcChannel.ForAddress(Address);
        grpcClient = new ImmuService.ImmuServiceClient(channel);
        shutdownTimeout = parameters.ShutdownTimeout;
    }

    public async Task ShutdownAsync()
    {
        if (channel == null)
        {
            return;
        }
        Task shutdownTask = channel.ShutdownAsync();
        await Task.WhenAny(shutdownTask, Task.Delay(shutdownTimeout));
        channel = null;
    }

    public void Shutdown()
    {
        if (channel == null)
        {
            return;
        }
        Task shutdownTask;
        using (ManualResetEvent mre = new ManualResetEvent(false))
        {
            shutdownTask = Task.Factory.StartNew(async () =>
            {
                try
                {
                    if (channel != null)
                    {
                        await channel.ShutdownAsync();
                    }
                }
                finally
                {
                    mre.Set();
                }
            });
            mre.WaitOne(shutdownTimeout);
        }
        channel = null;
    }
}

public class ReleasedConnection : IConnection
{
    public bool Released => true;

    public ReleasedConnection()
    {
        this.Address = "<not established>";
    }

    public ImmuService.ImmuServiceClient Service => throw new InvalidOperationException("The connection is not established.");
    public string Address { get; private set; }

    public Task ShutdownAsync()
    {
        return Task.CompletedTask;
    }
    
    public void Shutdown()
    {
    }
}