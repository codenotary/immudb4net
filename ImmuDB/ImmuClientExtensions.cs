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

namespace ImmudbProxy
{
    using Grpc.Core;
    using GrpcCore = global::Grpc.Core;

    public static partial class ImmuService
    {
        public partial class ImmuServiceClient : GrpcCore::ClientBase<ImmuServiceClient>
        {
            internal const string AUTH_HEADER = "authorization";

            public bool WithAuth { get; set; }
            public string AuthToken { get; set; }
            internal Metadata headers = new Metadata();

            public ImmuServiceClient WithAuthHeaders()
            {
                if (!WithAuth || (AuthToken == null))
                {
                   headers.Clear();
                   return this;
                }
                headers.Add(AUTH_HEADER, "Bearer " + AuthToken);
                return this;
            }
        }
    }
}