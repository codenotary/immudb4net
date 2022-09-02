
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
public class BuilderTests
{
    public ImmuClient? client;
    private string? tmpStateFolder;


    [TestMethod("new builder open method")]
    public async Task Test1()
    {
        tmpStateFolder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tmpStateFolder);

        FileImmuStateHolder stateHolder = FileImmuStateHolder.NewBuilder()
            .WithStatesFolder(tmpStateFolder)
            .build();

        ImmuClient.GlobalSettings.MaxConnectionsPerServer = 3;
        try
        {
            client = await ImmuClient.NewBuilder()
                .WithStateHolder(stateHolder)
                .WithServerUrl("localhost")
                .WithServerPort(3325)
                .WithCredentials("immudb", "immudb")
                .WithDatabase("defaultdb")
                .Open();
            byte[] v0 = new byte[] { 0, 1, 2, 3 };
            byte[] v1 = new byte[] { 3, 2, 1, 0 };

            TxHeader hdr0 = await client.Set("k0", v0);
            Assert.IsNotNull(hdr0);

            TxHeader hdr1 = await client.Set("k1", v1);
            Assert.IsNotNull(hdr1);

            Entry entry0 = await client.Get("k0");
            CollectionAssert.AreEqual(entry0.Value, v0);

            Entry entry1 = await client.Get("k1");
            CollectionAssert.AreEqual(entry1.Value, v1);

            Entry ventry0 = await client.VerifiedGet("k0");
            CollectionAssert.AreEqual(ventry0.Value, v0);

            Entry ventry1 = await client.VerifiedGet("k1");
            CollectionAssert.AreEqual(ventry1.Value, v1);

            byte[] v2 = new byte[] { 0, 1, 2, 3 };

            TxHeader hdr2 = await client.VerifiedSet("k2", v2);
            Assert.IsNotNull(hdr2);

            Entry ventry2 = await client.VerifiedGet("k2");
            CollectionAssert.AreEqual(v2, ventry2.Value);

            Entry e = await client.GetSinceTx("k2", hdr2.Id);
            Assert.IsNotNull(e);
            CollectionAssert.AreEqual(e.Value, v2);
        }
        finally
        {
            if (client != null)
            {
                await client.Close();
            }
        }
    }


    [TestMethod("simplified initialization")]
    public async Task Test2()
    {
        try
        {
            client = await ImmuClient.NewBuilder()
                .WithServerPort(3325)
                .Open();
            TxHeader hdr0 = await client.Set("k0", "v0");
            Assert.IsNotNull(hdr0);
            Entry entry0 = await client.Get("k0");
            Assert.AreEqual(entry0.ToString(), "v0");

        }
        finally
        {
            if (client != null)
            {
                await client.Close();
            }
        }
    }
    
    [TestMethod("simplified initialization with constructor")]
    public async Task Test3()
    {
        try
        {
            client = new ImmuClient("localhost", 3325);
            await client.Open("immudb", "immudb", "defaultdb");
            TxHeader hdr0 = await client.Set("k0", "v0");
            Assert.IsNotNull(hdr0);
            Entry entry0 = await client.Get("k0");
            Assert.AreEqual(entry0.ToString(), "v0");

        }
        finally
        {
            if (client != null)
            {
                await client.Close();
            }
        }
    }
    
    [TestMethod("StateHolder custom server key")]
    public async Task Test4()
    {
        try
        {
            client = new ImmuClient("localhost", 3325);
            await client.Open("immudb", "immudb", "defaultdb");
            TxHeader hdr0 = await client.Set("k0", "v0");
            Assert.IsNotNull(hdr0);
            Entry entry0 = await client.Get("k0");
            Assert.AreEqual(entry0.ToString(), "v0");

        }
        finally
        {
            if (client != null)
            {
                await client.Close();
            }
        }
    }

    [TestMethod("new builder with default state folder, open method and verified get/set")]
    public async Task Test5()
    {
        ImmuClient.GlobalSettings.MaxConnectionsPerServer = 3;
        try
        {
           client = await ImmuClient.NewBuilder().CheckDeploymentInfo(false)
                .WithServerPort(3325)
                .Open();
            byte[] v0 = new byte[] { 0, 1, 2, 3 };
            byte[] v1 = new byte[] { 3, 2, 1, 0 };

            TxHeader hdr0 = await client.Set("k0", v0);
            Assert.IsNotNull(hdr0);

            TxHeader hdr1 = await client.Set("k1", v1);
            Assert.IsNotNull(hdr1);

            Entry entry0 = await client.Get("k0");
            CollectionAssert.AreEqual(entry0.Value, v0);

            Entry entry1 = await client.Get("k1");
            CollectionAssert.AreEqual(entry1.Value, v1);

            Entry ventry0 = await client.VerifiedGet("k0");
            CollectionAssert.AreEqual(ventry0.Value, v0);

            Entry ventry1 = await client.VerifiedGet("k1");
            CollectionAssert.AreEqual(ventry1.Value, v1);

            byte[] v2 = new byte[] { 0, 1, 2, 3 };

            TxHeader hdr2 = await client.VerifiedSet("k2", v2);
            Assert.IsNotNull(hdr2);

            Entry ventry2 = await client.VerifiedGet("k2");
            CollectionAssert.AreEqual(v2, ventry2.Value);

            Entry e = await client.GetSinceTx("k2", hdr2.Id);
            Assert.IsNotNull(e);
            CollectionAssert.AreEqual(e.Value, v2);
        }
        finally
        {
            if (client != null)
            {
                await client.Close();
            }
        }
    }
}