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

/// <summary>
/// IConnection represents the abstraction for an ImmuClient Connection
/// </summary>
internal interface IConnection
{
    /// <summary>
    /// Gets the address
    /// </summary>
    /// <value></value>
    string Address { get; }
    /// <summary>
    /// Reference to the gRPC ImmuServiceClient
    /// </summary>
    /// <value></value>
    ImmuService.ImmuServiceClient Service { get; }
    /// <summary>
    /// awaitable shutdown of the connection
    /// </summary>
    /// <returns></returns>
    Task ShutdownAsync();
    /// <summary>
    /// Shutsdown the connection
    /// </summary>
    void Shutdown();
    /// <summary>
    /// Gets the boolean Released status of the connection
    /// </summary>
    /// <value></value>
    bool Released { get; }
}

/// <summary>
/// Represents the connection parameters
/// </summary>
public class ConnectionParameters
{
    /// <summary>
    /// Gets or sets the address
    /// </summary>
    /// <value></value>
    public string Address { get; set; } = "";
	public int MaxReceiveMessageSize { get; set; } = Int32.MaxValue;
    /// <summary>
    /// Gets or  sets the length of time the connection close operation is allowed to block before it completes.
    /// </summary>
    /// <value></value>
    public TimeSpan ShutdownTimeout { get; set; }
}

internal class Connection : IConnection
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
        channel = GrpcChannel.ForAddress(Address, new GrpcChannelOptions
        {
            MaxReceiveMessageSize = parameters.MaxReceiveMessageSize
        });
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
        shutdownTask = Task.Factory.StartNew(async () =>
        {
            if (channel != null)
            {
                await channel.ShutdownAsync();
            }
        });
        shutdownTask.Wait(shutdownTimeout);
        channel = null;
    }
}

internal class ReleasedConnection : IConnection
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
