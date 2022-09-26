
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
public class SyncTests
{
    public ImmuClientSync? client;
    private string? tmpStateFolder;


    [TestMethod("new builder open method")]
    public void Test1()
    {
        tmpStateFolder = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tmpStateFolder);

        FileImmuStateHolder stateHolder = FileImmuStateHolder.NewBuilder()
            .WithStatesFolder(tmpStateFolder)
            .Build();

        ImmuClient.GlobalSettings.MaxConnectionsPerServer = 3;
        try
        {
            client = ImmuClientSync.NewBuilder()
                .WithStateHolder(stateHolder)
                .WithServerUrl("localhost")
                .WithServerPort(3325)
                .WithCredentials("immudb", "immudb")
                .WithDatabase("defaultdb")
                .Open();
            byte[] v0 = new byte[] { 0, 1, 2, 3 };
            byte[] v1 = new byte[] { 3, 2, 1, 0 };

            TxHeader hdr0 = client.Set("k0", v0);
            Assert.IsNotNull(hdr0);

            TxHeader hdr1 = client.Set("k1", v1);
            Assert.IsNotNull(hdr1);

            Entry entry0 = client.Get("k0");
            CollectionAssert.AreEqual(entry0.Value, v0);

            Entry entry1 = client.Get("k1");
            CollectionAssert.AreEqual(entry1.Value, v1);

            Entry ventry0 = client.VerifiedGet("k0");
            CollectionAssert.AreEqual(ventry0.Value, v0);

            Entry ventry1 = client.VerifiedGet("k1");
            CollectionAssert.AreEqual(ventry1.Value, v1);

            byte[] v2 = new byte[] { 0, 1, 2, 3 };

            TxHeader hdr2 = client.VerifiedSet("k2", v2);
            Assert.IsNotNull(hdr2);

            Entry ventry2 = client.VerifiedGet("k2");
            CollectionAssert.AreEqual(v2, ventry2.Value);

            Entry e = client.GetSinceTx("k2", hdr2.Id);
            Assert.IsNotNull(e);
            CollectionAssert.AreEqual(e.Value, v2);
        }
        finally
        {
            if (client != null)
            {
                client.Close();
            }
        }
    }


    [TestMethod("simplified initialization")]
    public void Test2()
    {
        try
        {
            client = ImmuClientSync.NewBuilder()
                .WithServerPort(3325)
                .Open();
            TxHeader hdr0 = client.Set("k0", "v0");
            Assert.IsNotNull(hdr0);
            Entry entry0 = client.Get("k0");
            Assert.AreEqual(entry0.ToString(), "v0");

        }
        finally
        {
            if (client != null)
            {
                client.Close();
            }
        }
    }

    [TestMethod("simplified initialization with constructor")]
    public void Test3()
    {
        try
        {
            client = new ImmuClientSync("localhost", 3325);
            client.Open("immudb", "immudb", "defaultdb");
            TxHeader hdr0 = client.Set("k0", "v0");
            Assert.IsNotNull(hdr0);
            Entry entry0 = client.Get("k0");
            Assert.AreEqual(entry0.ToString(), "v0");

        }
        finally
        {
            if (client != null)
            {
                client.Close();
            }
        }
    }

    [TestMethod("StateHolder custom server key")]
    public void Test4()
    {
        try
        {
            client = new ImmuClientSync("localhost", 3325);
            client.Open("immudb", "immudb", "defaultdb");
            TxHeader hdr0 = client.Set("k0", "v0");
            Assert.IsNotNull(hdr0);
            Entry entry0 = client.Get("k0");
            Assert.AreEqual(entry0.ToString(), "v0");

        }
        finally
        {
            if (client != null)
            {
                client.Close();
            }
        }
    }

    [TestMethod("new builder with default state folder, open method and verified get/set")]
    public void Test5()
    {
        ImmuClient.GlobalSettings.MaxConnectionsPerServer = 3;
        try
        {
            client = ImmuClientSync.NewBuilder()
                 .WithServerPort(3325)
                 .Open();
            string hashedStateFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "immudb4net",
                    Utils.GenerateShortHash(client.GrpcAddress));
            if(Directory.Exists(hashedStateFolder))
            {
                Directory.Delete(hashedStateFolder, true);
            }

            byte[] v0 = new byte[] { 0, 1, 2, 3 };
            byte[] v1 = new byte[] { 3, 2, 1, 0 };

            TxHeader hdr0 = client.Set("k0", v0);
            Assert.IsNotNull(hdr0);

            TxHeader hdr1 = client.Set("k1", v1);
            Assert.IsNotNull(hdr1);

            Entry entry0 = client.Get("k0");
            CollectionAssert.AreEqual(entry0.Value, v0);

            Entry entry1 = client.Get("k1");
            CollectionAssert.AreEqual(entry1.Value, v1);

            Entry ventry0 = client.VerifiedGet("k0");
            CollectionAssert.AreEqual(ventry0.Value, v0);

            Entry ventry1 = client.VerifiedGet("k1");
            CollectionAssert.AreEqual(ventry1.Value, v1);

            byte[] v2 = new byte[] { 0, 1, 2, 3 };

            TxHeader hdr2 = client.VerifiedSet("k2", v2);
            Assert.IsNotNull(hdr2);

            Entry ventry2 = client.VerifiedGet("k2");
            CollectionAssert.AreEqual(v2, ventry2.Value);

            Entry e = client.GetSinceTx("k2", hdr2.Id);
            Assert.IsNotNull(e);
            CollectionAssert.AreEqual(e.Value, v2);
        }
        finally
        {
            if (client != null)
            {
                client.Close();
            }
        }
    }
}