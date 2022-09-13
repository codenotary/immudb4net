
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


namespace ImmuDB.Tests;

[TestClass]
public class ConnectionPoolTests
{
    public ImmuClient? client;
    private string? tmpStateFolder;


    [TestMethod("Idle Connection is closed")]
    [TestCategory("ConnectionPool")]
    public async Task Test1()
    {
        tmpStateFolder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tmpStateFolder);

        FileImmuStateHolder stateHolder = FileImmuStateHolder.NewBuilder()
            .WithStatesFolder(tmpStateFolder)
            .build();

        ImmuClient.GlobalSettings.MaxConnectionsPerServer = 2;
        ImmuClient.GlobalSettings.TerminateIdleConnectionTimeout = TimeSpan.FromMilliseconds(400);
        ImmuClient.GlobalSettings.IdleConnectionCheckInterval = TimeSpan.FromMilliseconds(200);
        await RandomAssignConnectionPool.ResetInstance();
        try
        {
            client = await ImmuClient.NewBuilder()
                .WithStateHolder(stateHolder)
                .WithServerUrl("localhost")
                .WithServerPort(3325)
                .WithCredentials("immudb", "immudb")
                .WithDatabase("defaultdb")
                .WithConnectionShutdownTimeout(TimeSpan.FromMilliseconds(50))
                .Open();
            byte[] v0 = new byte[] { 0, 1, 2, 3 };
            byte[] v1 = new byte[] { 3, 2, 1, 0 };

            TxHeader hdr0 = await client.Set("k0", v0);
            Assert.IsNotNull(hdr0);
        }
        finally
        {
            if (client != null)
            {
                await client.Close();
                // the connection is not immediately closed, will assert this as well
                Assert.IsFalse(client.Connection.Released);
            }
        }
        Thread.Sleep(TimeSpan.FromMilliseconds(1000));
        Assert.IsTrue(client.Connection.Released);
    }
}