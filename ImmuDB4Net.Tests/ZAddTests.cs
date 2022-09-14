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
public class ZAddTests : BaseClientIntTests
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

    [TestMethod("ZAdd, VerifiedZAdd and VerifiedZAddAt")]
    public async Task Test1()
    {
        await client!.Open("immudb", "immudb", "defaultdb");

        string set = "test-zadd";
        string key1 = "test-zadd-key1";
        byte[] val1 = Encoding.UTF8.GetBytes("val123");
        string key2 = "key2";
        byte[] val2 = Encoding.UTF8.GetBytes("val234");

        try
        {
            await client.Set(key1, val1);
            await client.Set(key2, val2);
        }
        catch (CorruptedDataException e)
        {
            Assert.Fail("Failed at set.", e);
        }

        TxHeader? txHdr = null;
        try
        {
            txHdr = await client.ZAdd(set, key1, 10);
            Assert.IsNotNull(txHdr);

            txHdr = await client.ZAdd(set, key2, 4);
            Assert.IsNotNull(txHdr);
        }
        catch (CorruptedDataException e)
        {
            Assert.Fail("Failed at zAdd.", e);
        }

        try
        {
            txHdr = await client.VerifiedZAdd(set, key2, 8);
        }
        catch (VerificationException e)
        {
            Assert.Fail("Failed at verifiedZAdd", e);
        }
        Assert.IsNotNull(txHdr);

        await client.Close();
    }
}