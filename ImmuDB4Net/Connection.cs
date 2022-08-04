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

using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using ImmudbProxy;
using static ImmuDB.ImmuClient;

namespace ImmuDB;

public interface IConnection
{
    IConnectionPool Pool { get; }
    string? ServerUUID { get; set; }
    ImmuService.ImmuServiceClient Service { get; }
    Task Shutdown();
}

public class Connection : IConnection
{
    private ImmuService.ImmuServiceClient grpcClient;
    public ImmuService.ImmuServiceClient Service => grpcClient;
    private GrpcChannel? channel;
    public IConnectionPool Pool { get; private set; }
    public string? ServerUUID { get; set; }
    internal HashSet<ImmuClient> Owners = new HashSet<ImmuClient>();

    internal Connection(Builder builder)
    {
        string schema = builder.ServerUrl.StartsWith("http") ? "" : "http://";
        var grpcAddress = $"{schema}{builder.ServerUrl}:{builder.ServerPort}";

        channel = GrpcChannel.ForAddress(grpcAddress);
        var invoker = channel.Intercept(new ImmuServerUUIDInterceptor(this));
        grpcClient = new ImmuService.ImmuServiceClient(invoker);
        Service.WithAuth = builder.Auth;
        Pool = builder.ConnectionPool;
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
}

public class ReleasedConnection : IConnection
{
    public IConnectionPool Pool {get; private set; }

    public ReleasedConnection(IConnectionPool pool)
    {
        this.Pool = pool;
    }

    public ImmuService.ImmuServiceClient Service => throw new InvalidOperationException("The connection has been released");
    public string? ServerUUID { get; set; }

    public Task Shutdown()
    {
        return Task.CompletedTask;
    }
}