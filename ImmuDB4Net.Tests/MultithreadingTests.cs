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
public class MultithreadingTests : BaseClientIntTests
{


    /// <summary>
    /// Gets or sets the test context which provides
    /// information about and functionality for the current test run.
    /// </summary>
    public TestContext? TestContext {get;set;}
    

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

    class IntHolder
    {
        public int succededCount;
    }

    [TestMethod("Multithreaded with key overlap")]
    public async Task Test1()
    {
        TestContext!.WriteLine("Start test");
        await client!.Open("immudb", "immudb", "defaultdb");

        int threadCount = 10;
        int keyCount = 10;

        CountdownEvent cde = new CountdownEvent(threadCount * keyCount);

        var intHolder = new IntHolder() {succededCount = 0};

        var action = async () =>
        {
            for (int i = 0; i < keyCount; i++)
            {
                Random rnd = new Random(Environment.TickCount);
                byte[] b = new byte[10];
                rnd.NextBytes(b);
                try
                {
                    await client.VerifiedSet("k" + i, b);
                }
                catch (Exception e)
                {
                    TestContext.WriteLine("Exception in verifiedset: {0}", e.ToString());
                    cde.Signal();
                    throw new InvalidOperationException();
                }
                Interlocked.Increment(ref intHolder.succededCount);
                cde.Signal();
            }
        };
        List<Task> tasks = new List<Task>(threadCount);
        for (int i = 0; i < threadCount; i++)
        {
            tasks.Add(Task.Factory.StartNew(action));
        }
        Task.WaitAll(tasks.ToArray());
        Assert.IsTrue(cde.Wait(TimeSpan.FromSeconds(12)));
        Assert.AreEqual(threadCount * keyCount, intHolder.succededCount);

        for (int i = 0; i < threadCount; i++)
        {
            for (int k = 0; k < keyCount; k++)
            {
                await client.VerifiedGet("k" + k);
            }
        }

        await client.Close();
    }
}