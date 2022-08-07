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

namespace ImmuDB.Tests
{
    [TestClass]
    public class BaseClientIntTests
    {
        public ImmuClient? client;

        public void BaseSetUp()
        {
            FileImmuStateHolder stateHolder = FileImmuStateHolder.NewBuilder()
                .WithStatesFolder("immudb/states")
                .build();

            client = ImmuClient.Builder()
                .WithStateHolder(stateHolder)
                .WithServerUrl("localhost")
                .WithServerPort(3322)
                .Build();
        }
        
        public void BaseSetUp(TimeSpan heartbeatInterval)
        {
            FileImmuStateHolder stateHolder = FileImmuStateHolder.NewBuilder()
                .WithStatesFolder("immudb/states")
                .build();

            client = ImmuClient.Builder()
                .WithStateHolder(stateHolder)
                .WithServerUrl("localhost")
                .WithServerPort(3322)
                .WithHeartbeatInterval(heartbeatInterval)
                .Build();
        }

        public async Task BaseTearDown()
        {
            await client!.Connection.Pool.Shutdown();
        }
    }
}