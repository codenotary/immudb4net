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

using Grpc.Core;
using Grpc.Core.Interceptors;
using static Grpc.Core.Metadata;

namespace ImmuDB;

public class ImmuServerUUIDInterceptor : Interceptor {

    private static readonly string SERVER_UUID = "immudb-uuid";
    private readonly ImmuClient client;

    public ImmuServerUUIDInterceptor(ImmuClient client) {
        this.client = client;
    }
    
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        Grpc.Core.Metadata.Entry? serverUuid = context.Options.Headers?.Get(SERVER_UUID);
        if((serverUuid != null) && !serverUuid.Value.Equals(client.CurrentServerUuid, StringComparison.InvariantCultureIgnoreCase)) {
            client.CurrentServerUuid = serverUuid.Value;
        } 
        return base.AsyncUnaryCall(request, context, continuation);
    }
}