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
public class SetAllGetAllTests : BaseClientIntTests
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

    [TestMethod("getall and setall")]
    public async Task Test6()
    {
        await client!.Login("immudb", "immudb");
        await client.UseDatabase("defaultdb");

        string key1 = "sga-key1";
        byte[] val1 = new byte[] { 1 };
        string key2 = "sga-key2";
        byte[] val2 = new byte[] { 2, 3 };
        string key3 = "sga-key3";
        byte[] val3 = new byte[] { 3, 4, 5 };

        List<KVPair> kvs = new List<KVPair>() {
            new KVPair(key1, val1),
            new KVPair(key2, val2),
            new KVPair(key3, val3),
        };

        try
        {
            TxHeader txMd = await client.SetAll(kvs);
            Assert.IsNotNull(txMd);
        }
        catch (CorruptedDataException e)
        {
            Assert.Fail("Failed at SetAll.", e);
        }

        List<String> keys = new List<string>() {key1, key2, key3};
        List<Entry> got = await client.GetAll(keys);

        Assert.AreEqual(kvs.Count, got.Count);

        for (int i = 0; i < kvs.Count; i++)
        {
            CollectionAssert.AreEqual(kvs[i].Value, got[i].Value, String.Format("Expected: %s got: %s", kvs[i], got[i]));
        }

        await client.Logout();
    }
}