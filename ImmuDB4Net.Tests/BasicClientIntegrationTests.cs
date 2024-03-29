
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
public class BasicClientIntegrationTests : BaseClientIntegrationTests
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

    [TestMethod("execute open, set, get and verifiedget")]
    public async Task BasicTest()
    {
        await client!.Open("immudb", "immudb", "defaultdb");
        await client.VerifiedSet("mykey", Encoding.UTF8.GetBytes("test"));
        await client.Close();
        await client!.Open("immudb", "immudb", "defaultdb");
        await client.Close();
    }

    [TestMethod("execute open, set, get and verifiedget")]
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

        await client.Close();
    }

    [TestMethod("execute login, setall, get and getall")]
    public async Task Test2()
    {
        await client!.Open("immudb", "immudb", "defaultdb");
        List<string> keys = new List<string>();
        keys.Add("k0");
        keys.Add("k1");

        List<byte[]> values = new List<byte[]>();
        values.Add(new byte[] { 0, 1, 0, 1 });
        values.Add(new byte[] { 1, 0, 1, 0 });

        List<KVPair> kvListBuilder = new List<KVPair>();

        for (int i = 0; i < keys.Count; i++)
        {
            kvListBuilder.Add(new KVPair(keys[i], values[i]));
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

        await client.Close();
    }

    [TestMethod("execute open, verifiedset and verifiedget")]
    public async Task TestSimpleVerifiedSetAndGet()
    {
        await client!.Open("immudb", "immudb", "defaultdb");

        byte[] v0 = new byte[] { 0, 1, 2, 3 };
        byte[] v1 = new byte[] { 3, 2, 1, 0 };

        TxHeader hdr0 = await client.VerifiedSet("k0", v0);
        Assert.IsNotNull(hdr0);

        Entry ventry0 = await client.VerifiedGet("k0");
        CollectionAssert.AreEqual(ventry0.Value, v0);

        TxHeader hdr1 = await client.VerifiedSet("key1", "value1");
        Assert.IsNotNull(hdr1);

        Entry ventry1 = await client.VerifiedGet("key1");
        Assert.AreEqual("value1", ventry1.ToString());
        await client.Close();
    }    
    
    [TestMethod("execute expirableset and verifiedget")]
    public async Task TestExpirableSet()
    {
        await client!.Open("immudb", "immudb", "defaultdb");

        byte[] v0 = new byte[] { 0, 1, 2, 3 };
        byte[] v1 = new byte[] { 3, 2, 1, 0 };

        TxHeader hdr0 = await client.ExpirableSet("k0", v0, DateTime.Now.AddDays(1));
        Assert.IsNotNull(hdr0);

        Entry ventry0 = await client.VerifiedGet("k0");
        CollectionAssert.AreEqual(ventry0.Value, v0);

        await client.ExpirableSet("key1", "value1", DateTime.Now.AddDays(1));
        Entry ventry1 = await client.VerifiedGet("key1");
        Assert.AreEqual("value1", ventry1.ToString());

        await client.ExpirableSet("key2", "value2", DateTime.Now.AddDays(-1));
        try 
        {
            Entry ventry2 = await client.VerifiedGet("tempkey");
            Assert.Fail("tempkey should not be present");
        }
        catch(KeyNotFoundException) {
        }
        await client.Close();
    }

    [TestMethod("execute sqlexec and query")]
    public async Task Test3()
    {
        await client!.Open("immudb", "immudb", "defaultdb");
        var rspCreate = await client.SQLExec("CREATE TABLE IF NOT EXISTS logs(id INTEGER AUTO_INCREMENT, created TIMESTAMP, entry VARCHAR, PRIMARY KEY id)");
        Assert.AreEqual(1, rspCreate.Items.Count);
        var rspIndex = await client.SQLExec("CREATE INDEX IF NOT EXISTS ON logs(created)");
        Assert.AreEqual(1, rspIndex.Items.Count);
        var rspInsert = await client.SQLExec("INSERT INTO logs(created, entry) VALUES($1, $2)",
            SQL.SQLParameter.Create(DateTime.UtcNow),
            SQL.SQLParameter.Create("entry1"));
        var queryResult = await client.SQLQuery("SELECT created, entry FROM LOGS order by created DESC");
        Assert.AreEqual(2, queryResult.Columns.Count);
        Assert.AreEqual(1, queryResult.Rows.Count);
        var sqlVal = queryResult.Rows[0]["entry"];
        Assert.AreEqual("entry1", (string)sqlVal.Value);
        await client.Close();
    }
}