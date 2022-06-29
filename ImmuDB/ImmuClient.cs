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

public class ImmuClient
{

    private readonly AsymmetricKeyParameter serverSigningKey;
    private readonly bool withAuth;
    private readonly ImmuStateHolder stateHolder;

    public static Builder NewBuilder()
    {
        return new Builder();
    }

    public ImmuClient(Builder builder)
    {
        this.withAuth = builder.Auth;
        this.serverSigningKey = builder.ServerSigningKey;
        this.stateHolder = builder.StateHolder;
    }

    public class Builder
    {
        public String ServerUrl { get; private set; }
        public int ServerPort { get; private set; }
        public AsymmetricKeyParameter ServerSigningKey { get; private set; }
        public bool Auth { get; private set; }
        public ImmuStateHolder StateHolder {get; private set; }

        public Builder()
        {
            ServerUrl = "localhost";
            ServerPort = 3322;
            StateHolder = new SerializableImmuStateHolder();
            Auth = true;
        }

        public Builder WithAuth(bool withAuth) {
            this.Auth = withAuth;
            return this;
        }
        
        public Builder WithServerPort(int serverPort) {
            this.ServerPort = serverPort;
            return this;
        }
        
        public Builder WithServerUrl(String serverUrl) {
            this.ServerUrl = serverUrl;
            return this;
        }

        public Builder WithServerSigningKey(String publicKeyFileName)
        {
            this.ServerSigningKey = ImmuState.GetPublicKeyFromPemFile(publicKeyFileName);
            return this;
        }
    }
}