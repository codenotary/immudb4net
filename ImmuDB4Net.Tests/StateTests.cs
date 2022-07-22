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

using System.Reflection;
using ImmuDB.Exceptions;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;

namespace ImmuDB.Tests;

[TestClass]
public class StateTests : BaseClientIntTests
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

    [TestMethod("current state")]
    public async Task Test1()
    {
        await client!.Login("immudb", "immudb");
        await client.UseDatabase("defaultdb");

        ImmuState currState = await client.CurrentState();
        Assert.IsNotNull(currState);

        Assembly asm = Assembly.GetExecutingAssembly();
        string resourceName = "ImmuDB4Net.Tests.resources.test_public_key.pem";

        try
        {
            using (Stream? stream = asm.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    Assert.Fail("Could not read resource");
                }
                using (TextReader tr = new StreamReader(stream))
                {
                    PemReader pemReader = new PemReader(tr);
                    AsymmetricKeyParameter assymKey = (AsymmetricKeyParameter)pemReader.ReadObject();
                    // The signature verification in this case should fail
                    Assert.IsFalse(currState.CheckSignature(assymKey));
                    // Again, "covering" `checkSignature` when there is a `signature` attached.
                    ImmuState someState = new ImmuState(currState.Database, currState.TxId, currState.TxHash, new byte[1]);
                    Assert.IsFalse(someState.CheckSignature(assymKey));
                    await client.Logout();
                }
            }
        }
        catch (Exception e)
        {
            await client.Logout();
            Assert.Fail(string.Format("An exception occurred in StateTests->Test1. %s", e.ToString()));
            return;
        }
    }

    [TestMethod("currentState with server signature checking, but only on the client side")]
    public async Task Test2()
    {
        Assembly asm = Assembly.GetExecutingAssembly();
        string resourceName = "ImmuDB4Net.Tests.resources.test_public_key.pem";
        AsymmetricKeyParameter assymKey;
        ImmuClient client;
        try
        {
            using (Stream? stream = asm.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    Assert.Fail("Could not read resource");
                }
                using (TextReader tr = new StreamReader(stream))
                {
                    PemReader pemReader = new PemReader(tr);
                    assymKey = (AsymmetricKeyParameter)pemReader.ReadObject();
                }
            }
            client = ImmuClient.NewBuilder()
                        .WithServerUrl("localhost")
                        .WithServerPort(3322)
                        .WithServerSigningKey(assymKey)
                        .Build();
        }
        catch (Exception e)
        {
            Assert.Fail(string.Format("An exception occurred in StateTests->Test1. %s", e.ToString()));
            return;
        }
        
        await client.Login("immudb", "immudb");
        await client.UseDatabase("defaultdb");
        try 
        {
            await client.CurrentState();
            Assert.Fail("Signing key provided on the client side only and currentstate should raise verificationexception");
        }
        catch(VerificationException) {

        }
        await client.Logout();
    }
}