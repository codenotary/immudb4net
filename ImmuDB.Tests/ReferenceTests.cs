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
public class ReferenceTests : BaseClientIntTests
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

    [TestMethod("set, setreference and getreference")]
    public async Task Test7()
    {
        await client!.Login("immudb", "immudb");
        await client.UseDatabase("defaultdb");

        byte[] key = Encoding.UTF8.GetBytes("testRef");
        byte[] val = Encoding.UTF8.GetBytes("abc");

        TxHeader? setTxHdr = null;
        try {
            setTxHdr = await client.Set(key, val);
        } catch (CorruptedDataException e) {
            Assert.Fail("Failed at set.", e);
        }

        byte[] ref1Key = Encoding.UTF8.GetBytes("ref1_to_testRef");
        byte[] ref2Key = Encoding.UTF8.GetBytes("ref2_to_testRef");

        TxHeader? ref1TxHdr = null;
        try {
            ref1TxHdr = await client.SetReference(ref1Key, key);
        } catch (CorruptedDataException e) {
            Assert.Fail("Failed at setReference", e);
        }
        Assert.IsNotNull(ref1TxHdr);

        TxHeader? ref2TxHdr = null;
        try {
            ref2TxHdr = await client.SetReference(ref2Key, key, setTxHdr.Id);
        } catch (CorruptedDataException e) {
            Assert.Fail("Failed at setReferenceAt.", e);
        }
        Assert.IsNotNull(ref2TxHdr);

        await client.Logout();
    }
}