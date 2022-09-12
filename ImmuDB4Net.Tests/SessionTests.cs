
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
public class SessionTests : BaseClientIntTests
{
    [TestInitialize]
    public void SetUp()
    {
        BaseSetUp(TimeSpan.FromSeconds(2));
    }

    [TestCleanup]
    public async Task TearDown()
    {
        await BaseTearDown();
    }

    [TestMethod("execute open, set, get check if heartbeat is executed")]
    public async Task Test1()
    {
        await client!.Open("immudb", "immudb", "defaultdb");

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
        Assert.IsTrue(client.heartbeatCalled?.WaitOne(TimeSpan.FromSeconds(3)));

        await client.Close();
    }
    
    [TestMethod("OpenSession second call generates exception")]
    [ExpectedException(typeof(InvalidOperationException))]
    public async Task Test2()
    {
        await client!.Open("immudb", "immudb", "defaultdb");
        try{
            await client!.Open("immudb", "immudb", "defaultdb");
        }
        finally
        {
            await client.Close();
        }
    }

    [TestMethod("execute open, set, get, reconnect, check if heartbeat is executed")]
    public async Task Test3()
    {
        await client!.Open("immudb", "immudb", "defaultdb");

        byte[] v0 = new byte[] { 0, 1, 2, 3 };
        byte[] v1 = new byte[] { 3, 2, 1, 0 };

        TxHeader hdr0 = await client.Set("k0", v0);
        Assert.IsNotNull(hdr0);

        await client.Reconnect();

        TxHeader hdr1 = await client.Set("k1", v1);
        Assert.IsNotNull(hdr1);

        Entry entry0 = await client.Get("k0");
        CollectionAssert.AreEqual(entry0.Value, v0);

        Entry entry1 = await client.Get("k1");
        CollectionAssert.AreEqual(entry1.Value, v1);
        Assert.IsTrue(client.heartbeatCalled?.WaitOne(TimeSpan.FromSeconds(3)));

        await client.Close();
    }
}