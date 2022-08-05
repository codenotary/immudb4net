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
public class ScanTests : BaseClientIntTests
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

    [TestMethod("execute set and scan")]
    public async Task Test4()
    {

        await client!.Open("immudb", "immudb", "defaultdb");

        byte[] value1 = { 0, 1, 2, 3 };
        byte[] value2 = { 4, 5, 6, 7 };

        try
        {
            await client.Set("scan1", value1);
            await client.Set("scan2", value2);
        }
        catch (CorruptedDataException e)
        {
            Assert.Fail("Failed at set.", e);
        }

        List<Entry> scanResult = await client.Scan("scan", 5, false);

        Assert.AreEqual(scanResult.Count, 2);
        CollectionAssert.AreEqual(scanResult[0].Key, Encoding.UTF8.GetBytes("scan1"));
        CollectionAssert.AreEqual(scanResult[0].Value, value1);
        CollectionAssert.AreEqual(scanResult[1].Key, Encoding.UTF8.GetBytes("scan2"));
        CollectionAssert.AreEqual(scanResult[1].Value, value2);

        Assert.IsTrue((await client.Scan("scan")).Count > 0);

        Assert.AreEqual((await client.Scan("scan", "scan1", 1, false)).Count, 1);

        await client.Close();
    }

    [TestMethod("execute set, zadd, zscan")]
    public async Task Test1()
    {
        await client!.Open("immudb", "immudb", "defaultdb");

        byte[] value1 = { 0, 1, 2, 3 };
        byte[] value2 = { 4, 5, 6, 7 };

        try
        {
            await client.Set("zadd1", value1);
            await client.Set("zadd2", value2);
        }
        catch (CorruptedDataException e)
        {
            Assert.Fail("Failed at set.", e);
        }

        try
        {
            await client.ZAdd("set1", "zadd1", 1);
            await client.ZAdd("set1", "zadd2", 2);

            await client.ZAdd("set2", "zadd1", 2);
            await client.ZAdd("set2", "zadd2", 1);
        }
        catch (CorruptedDataException e)
        {
            Assert.Fail("Failed to zAdd", e);
        }

        List<ZEntry> zScan1 = await client.ZScan("set1", 5, false);
        
        Assert.AreEqual(zScan1.Count, 2);

        CollectionAssert.AreEqual(zScan1[0].Key, Encoding.UTF8.GetBytes("zadd1"));
        CollectionAssert.AreEqual(zScan1[0].Entry.Value, value1);

        List<ZEntry> zScan2 = await client.ZScan("set2", 5, false);
        Assert.AreEqual(zScan2.Count, 2);

        await client.Close();
    }
}