
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

using System.Text;
using ImmuDB.Exceptions;

namespace ImmuDB.Tests;

[TestClass]
public class BasicClientTests : BaseClientIntTests
{
    [TestInitialize]
    public void SetUp()
    {
        BaseSetUp();
    }

    [TestCleanup]
    public async Task TearDown()
    {
        await BaseTearDown();
    }


    [TestMethod("execute login, usedatabase, set, get and verifiedget")]
    public async Task Test1()
    {
        await client!.Login("immudb", "immudb");
        await client.UseDatabase("defaultdb");

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

        await client.Logout();
    }

    [TestMethod("execute login, usedatabase, set, get and verifiedget")]
    public async Task Test2()
    {
        await client!.Login("immudb", "immudb");
        await client.UseDatabase("defaultdb");
        List<String> keys = new List<String>();
        keys.Add("k0");
        keys.Add("k1");

        List<byte[]> values = new List<byte[]>();
        values.Add(new byte[] { 0, 1, 0, 1 });
        values.Add(new byte[] { 1, 0, 1, 0 });

        List<KVPair> kvListBuilder = new List<KVPair>();

        for (int i = 0; i < keys.Count; i++)
        {
            kvListBuilder.Add(new KVPair(keys[i],values[i]));
        }

        try
        {
            await client.SetAll(kvListBuilder);
        }
        catch (CorruptedDataException e)
        {
            Assert.Fail("Failed at setAll.", e);
        }

        List<Entry> getAllResult = await client.GetAll(keys);

        Assert.IsNotNull(getAllResult);
        Assert.AreEqual(keys.Count, getAllResult.Count);

        for (int i = 0; i < getAllResult.Count; i++)
        {
            Entry entry = getAllResult[i];
            CollectionAssert.AreEqual(entry.Key, Encoding.UTF8.GetBytes(keys[i]));
            CollectionAssert.AreEqual(entry.Value, values[i]);
        }

        for (int i = 0; i < keys.Count; i++)
        {
            Entry entry = await client.Get(keys[i]);
            CollectionAssert.AreEqual(entry.Value, values[i]);
        }

        await client.Logout();
    }

}