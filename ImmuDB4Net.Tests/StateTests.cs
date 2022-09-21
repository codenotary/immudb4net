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

using System.Reflection;
using Docker.DotNet;
using Docker.DotNet.Models;
using ImmuDB.Exceptions;
using Mono.Unix;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;

namespace ImmuDB.Tests;

[TestClass]
public class StateTests : BaseClientIntTests
{

    [TestInitialize]
    public async Task SetUp()
    {
        await BaseSetUp();
    }

    [TestCleanup]
    public async Task TearDown()
    {
        await BaseTearDown();
    }

    [TestMethod("current state")]
    public async Task Test1()
    {
        await client!.Open("immudb", "immudb", "defaultdb");

        ImmuState currState = client.CurrentState;
        Assert.IsNotNull(currState);

        Assembly asm = Assembly.GetExecutingAssembly();
        string resourceName = "ImmuDB4Net.Tests.resources.test_public_key.pem";

        try
        {
            using (Stream? stream = asm.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    Assert.Fail("Could not read resource");
                }
                using (TextReader tr = new StreamReader(stream))
                {
                    PemReader pemReader = new PemReader(tr);
                    AsymmetricKeyParameter assymKey = (AsymmetricKeyParameter)pemReader.ReadObject();
                    // The signature verification in this case should fail
                    Assert.IsFalse(currState.CheckSignature(assymKey));
                    // Again, "covering" `checkSignature` when there is a `signature` attached.
                    ImmuState someState = new ImmuState(currState.Database, currState.TxId, currState.TxHash, new byte[1]);
                    Assert.IsFalse(someState.CheckSignature(assymKey));
                    await client.Close();
                }
            }
        }
        catch (Exception e)
        {
            await client.Close();
            Assert.Fail(string.Format("An exception occurred in StateTests->Test1. {0}", e.ToString()));
            return;
        }
    }

    [TestMethod("currentState with server signature checking, but only on the client side")]
    public async Task Test2()
    {
        Assembly asm = Assembly.GetExecutingAssembly();
        string resourceName = "ImmuDB4Net.Tests.resources.test_public_key.pem";
        AsymmetricKeyParameter assymKey;
        ImmuClient client;
        try
        {
            using (Stream? stream = asm.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    Assert.Fail("Could not read resource");
                }
                using (TextReader tr = new StreamReader(stream))
                {
                    PemReader pemReader = new PemReader(tr);
                    assymKey = (AsymmetricKeyParameter)pemReader.ReadObject();
                }
            }
            client = ImmuClient.NewBuilder()
                        .WithServerUrl("localhost")
                        .WithServerPort(3325)
                        .WithServerSigningKey(assymKey)
                        .Build();
        }
        catch (Exception e)
        {
            Assert.Fail(string.Format("An exception occurred in StateTests->Test1. {0}", e.ToString()));
            return;
        }

        await client.Open("immudb", "immudb", "defaultdb");
        try
        {
            _ = client.CurrentState;
            Assert.Fail("Signing key provided on the client side only and currentstate should raise verificationexception");
        }
        catch (VerificationException)
        {

        }
        await client.Close();
    }

    private string CreatePrivateKeyInTmpFolder()
    {
        Assembly asm = Assembly.GetExecutingAssembly();
        string resourceName = "ImmuDB4Net.Tests.resources.test_private_key.pem";
        using (Stream? stream = asm.GetManifestResourceStream(resourceName))
        {
            if (stream == null)
            {
                Assert.Fail("Could not read resource");
            }
            using (TextReader tr = new StreamReader(stream))
            {
                string content = tr.ReadToEnd();
                string tmpPath = Path.GetTempFileName();
                File.WriteAllText(tmpPath, content);
                var fileInfo = new UnixFileInfo(tmpPath);
                fileInfo.FileAccessPermissions =
                    FileAccessPermissions.UserRead | FileAccessPermissions.UserWrite
                    | FileAccessPermissions.GroupRead
                    | FileAccessPermissions.OtherRead;
                return tmpPath;
            }
        }
    }


    [TestMethod(@"start docker container with immudb then check currentState with server and client signature checking. In order to successfully run this 
    test you need to have docker installed.")]
    public async Task Test3()
    {
        string tmpFile = CreatePrivateKeyInTmpFolder();
        string containerId = "";
        bool containerHasStarted = false;
        DockerClient dockerClient = new DockerClientConfiguration().CreateClient();

        try
        {
            var createRsp = await dockerClient.Containers.CreateContainerAsync(new Docker.DotNet.Models.CreateContainerParameters()
            {
                Image = "codenotary/immudb",
                Env = new List<string>() { "IMMUDB_SIGNINGKEY=/key/test_private_key.pem", "IMMUDB_PORT=3323" },
                HostConfig = new HostConfig
                {
                    Binds = new[] { $"{tmpFile}:/key/test_private_key.pem" },
                    PortBindings = new Dictionary<string, IList<PortBinding>> {
                        {"3323/tcp", new List<PortBinding> {new PortBinding() {HostPort="3323"}} },
                    },
                },
                ExposedPorts = new Dictionary<string, EmptyStruct> {
                    {"3323/tcp", new EmptyStruct() }
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
            Assembly asm = Assembly.GetExecutingAssembly();
            string resourceName = "ImmuDB4Net.Tests.resources.test_public_key.pem";
            AsymmetricKeyParameter assymKey;
            ImmuClient client;
            try
            {
                using (Stream? stream = asm.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        Assert.Fail("Could not read resource");
                    }
                    using (TextReader tr = new StreamReader(stream))
                    {
                        PemReader pemReader = new PemReader(tr);
                        assymKey = (AsymmetricKeyParameter)pemReader.ReadObject();
                    }
                }
                client = ImmuClient.NewBuilder()
                            .WithServerUrl("localhost")
                            .WithServerPort(3323)
                            .WithServerSigningKey(assymKey)
                            .CheckDeploymentInfo(false)
                            .Build();
            }
            catch (Exception e)
            {
                Assert.Fail(string.Format("An exception occurred in StateTests->Test3. {0}", e.ToString()));
                return;
            }

            await client.Open("immudb", "immudb", "defaultdb");
            try
            {
                var state = client.CurrentState;
                Assert.IsNotNull(state);
            }
            catch (VerificationException)
            {
                Assert.Fail("Signing key provided on the client side and server and currentstate should work");
            }
            await client.Close();
        }
        finally
        {
            File.Delete(tmpFile);
            if (containerHasStarted)
            {
                await dockerClient.Containers.StopContainerAsync(containerId, new ContainerStopParameters() { WaitBeforeKillSeconds = 6 });
                await dockerClient.Containers.RemoveContainerAsync(containerId, new ContainerRemoveParameters() { Force = true });

            }
        }
    }


}