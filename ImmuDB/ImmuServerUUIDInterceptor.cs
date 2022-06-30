using Grpc.Core;
using Grpc.Core.Interceptors;
using static Grpc.Core.Metadata;

namespace ImmuDB;

public class ImmuServerUUIDInterceptor : Interceptor {

    private static readonly String SERVER_UUID = "immudb-uuid";
    private readonly ImmuClient client;

    public ImmuServerUUIDInterceptor(ImmuClient client) {
        this.client = client;
    }
    
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        Entry? serverUuid = context.Options.Headers?.Get(SERVER_UUID);
        if((serverUuid != null) && !serverUuid.Value.Equals(client.CurrentServerUuid, StringComparison.InvariantCultureIgnoreCase)) {
            client.CurrentServerUuid = serverUuid.Value;
        } 
        return base.AsyncUnaryCall(request, context, continuation);
    }
}