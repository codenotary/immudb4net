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

using Docker.DotNet;
using Docker.DotNet.Models;

namespace ImmuDB.Tests
{
    [TestClass]
    public class BaseClientIntTests
    {
        public ImmuClient? client;
        private string? tmpStateFolder;
        private static string containerId = "";
        private static bool containerHasStarted = false;
        private static DockerClient? dockerClient;

        public async Task BaseSetUp()
        {
            tmpStateFolder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tmpStateFolder);

            FileImmuStateHolder stateHolder = FileImmuStateHolder.NewBuilder()
                .WithStatesFolder(tmpStateFolder)
                .build();

            await RandomAssignConnectionPool.ResetInstance();

            ImmuClient.GlobalSettings.MaxConnectionsPerServer = 3;
            client = ImmuClient.NewBuilder()
                .WithStateHolder(stateHolder)
                .WithServerUrl("localhost")
                .WithServerPort(3325)
                .Build();
        }

        public async Task BaseSetUp(TimeSpan heartbeatInterval)
        {
            tmpStateFolder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tmpStateFolder);
            FileImmuStateHolder stateHolder = FileImmuStateHolder.NewBuilder()
                .WithStatesFolder(tmpStateFolder)
                .build();
            await RandomAssignConnectionPool.ResetInstance();

            ImmuClient.GlobalSettings.MaxConnectionsPerServer = 3;
            client = ImmuClient.NewBuilder()
                .WithStateHolder(stateHolder)
                .WithServerUrl("localhost")
                .WithServerPort(3325)
                .WithHeartbeatInterval(heartbeatInterval)
                .Build();
        }

        public async Task BaseTearDown()
        {
            if (tmpStateFolder != null)
            {
                Directory.Delete(tmpStateFolder, true);
            }
            await Task.Yield();
        }
        [AssemblyInitialize]
        public static async Task AssemblySetUp(TestContext testContext)
        {
            dockerClient = new DockerClientConfiguration().CreateClient();
            try
            {
                var createRsp = await dockerClient.Containers.CreateContainerAsync(new Docker.DotNet.Models.CreateContainerParameters()
                {
                    Image = "codenotary/immudb",
                    Env = new List<string>() { "IMMUDB_PORT=3325" },
                    HostConfig = new HostConfig
                    {
                        PortBindings = new Dictionary<string, IList<PortBinding>> {
                        {"3325/tcp", new List<PortBinding> {new PortBinding() {HostPort="3325"}} },
                    },
                    },
                    ExposedPorts = new Dictionary<string, EmptyStruct> {
                    {"3325/tcp", new EmptyStruct() }
                }
                });
                containerId = createRsp.ID;
                containerHasStarted = await dockerClient.Containers.StartContainerAsync(containerId, new ContainerStartParameters());
                if (!containerHasStarted)
                {
                    Assert.Fail("Could not start the immudb container");
                    return;
                }
                //wait for ImmuDB to start
                Thread.Sleep(2500);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("Could not create ImmuDB container, please make sure docker is installed and running properly.", e);
            }
        }

        [AssemblyCleanup]
        public static async Task AssemblyTearDown()
        {
            await ImmuClient.ReleaseSdkResources();
            if (containerHasStarted)
            {
                await dockerClient!.Containers.StopContainerAsync(containerId, new ContainerStopParameters() { WaitBeforeKillSeconds = 6 });
                await dockerClient.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters() { Force = true });
            }
        }
    }
}