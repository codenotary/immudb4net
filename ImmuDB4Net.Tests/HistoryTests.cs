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
public class HistoryTests : BaseClientIntTests
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

    [TestMethod("execute set, history")]
    public async Task Test1()
    {

        await client!.Open("immudb", "immudb", "defaultdb");

        byte[] value1 = { 0, 1, 2, 3 };
        byte[] value2 = { 4, 5, 6, 7 };
        byte[] value3 = { 8, 9, 10, 11 };



        try
        {
            await client.Set("history1", value1);
            await client.Set("history1", value2);
            await client.Set("history2", value1);
            await client.Set("history2", value2);
            await client.Set("history2", value3);
        }
        catch (CorruptedDataException e)
        {
            Assert.Fail("Failed at set.", e);
        }

        List<Entry> historyResponse1 = await client.History("history1", 10, 0, false);

        Assert.AreEqual(2, historyResponse1.Count);

        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("history1"), historyResponse1[0].Key);
        CollectionAssert.AreEqual(value1, historyResponse1[0].Value);

        CollectionAssert.AreEqual(historyResponse1[1].Key, Encoding.UTF8.GetBytes("history1"));
        CollectionAssert.AreEqual(historyResponse1[1].Value, value2);

        List<Entry> historyResponse2 = await client.History("history2", 10, 0, false);

        Assert.AreEqual(historyResponse2.Count, 3);

        CollectionAssert.AreEqual(historyResponse2[0].Key, Encoding.UTF8.GetBytes("history2"));
        CollectionAssert.AreEqual(historyResponse2[0].Value, value1);

        CollectionAssert.AreEqual(historyResponse2[1].Key, Encoding.UTF8.GetBytes("history2"));
        CollectionAssert.AreEqual(historyResponse2[1].Value, value2);

        CollectionAssert.AreEqual(historyResponse2[2].Key, Encoding.UTF8.GetBytes("history2"));
        CollectionAssert.AreEqual(historyResponse2[2].Value, value3);

        historyResponse2 = await client.History("history2", 10, 2, false);
        Assert.IsNotNull(historyResponse2);
        Assert.AreEqual(historyResponse2.Count, 1);

        try
        {
            await client.History("nonExisting", 10, 0, false);
            Assert.Fail("key not found exception expected");
        }
        catch (KeyNotFoundException)
        {
            // exception is expected here
        }

        await client.Close();
    }

}