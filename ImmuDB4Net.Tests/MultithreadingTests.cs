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

using System.Collections.Concurrent;
using System.Text;
using ImmuDB.Exceptions;

namespace ImmuDB.Tests;

[TestClass]
public class MultithreadingTests : BaseClientIntegrationTests
{


    /// <summary>
    /// Gets or sets the test context which provides
    /// information about and functionality for the current test run.
    /// </summary>
    public TestContext? TestContext { get; set; }


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

    class IntHolder
    {
        public int succededCount;
    }

    [TestMethod("Multithreaded with key overlap")]
    public async Task Test1()
    {
        TestContext!.WriteLine("Start test1");
        await client!.Open("immudb", "immudb", "defaultdb");

        int threadCount = 10;
        int keyCount = 10;

        CountdownEvent cde = new CountdownEvent(threadCount * keyCount);

        var intHolder = new IntHolder() { succededCount = 0 };

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

    [TestMethod("Multiple open sessions and close sessions")]
    public void ImmuClientSyncMultipleInstances()
    {
        TestContext!.WriteLine("Start test2");
        string hashedStateFolder = Path.Combine(
               Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
               "immudb4net",
               Utils.GenerateShortHash("http://localhost:3325"));
        if (Directory.Exists(hashedStateFolder))
        {
            Directory.Delete(hashedStateFolder, true);
        }

        ConcurrentStack<ImmuClientSync> clients = new ConcurrentStack<ImmuClientSync>();
        List<Action> actions = new List<Action>();
        List<Task> tasks = new List<Task>();
        int threadCount = 10;
        for (int i = 0; i < threadCount; i++)
        {
            var i1 = i;
            actions.Add(() =>
            {
                var localclient = ImmuClientSync.NewBuilder()
                 .WithServerPort(3325)
                 .Open();
                clients.Push(localclient);
                localclient.VerifiedSet("key" + i1, "val" + i1);
                Entry readEntry = localclient.VerifiedGet("key" + i1);
                Assert.AreEqual("val" + i1, Encoding.UTF8.GetString(readEntry.Value));
                localclient.Close();
            });
        }
        foreach (var action in actions)
        {
            tasks.Add(Task.Factory.StartNew(action));
        }
        try
        {
            Task.WaitAll(tasks.ToArray());
        }
        catch (AggregateException e)
        {
            TestContext!.WriteLine("\nThe following exceptions have been thrown by WaitAll():");
            for (int j = 0; j < e.InnerExceptions.Count; j++)
            {
                TestContext!.WriteLine("\n-------------------------------------------------\n{0}", e.InnerExceptions[j].ToString());
            }
        }
        TestContext!.WriteLine("Done.");
    }

    [TestMethod("Multiple open sessions and close sessions")]
    public async Task ImmuClientMultipleInstances()
    {
        TestContext!.WriteLine("Start test2");
        string hashedStateFolder = Path.Combine(
               Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
               "immudb4net",
               Utils.GenerateShortHash("http://localhost:3325"));
        if (Directory.Exists(hashedStateFolder))
        {
            Directory.Delete(hashedStateFolder, true);
        }

        ConcurrentStack<ImmuClient> clients = new ConcurrentStack<ImmuClient>();
        List<Func<Task>> actions = new List<Func<Task>>();
        List<Task> tasks = new List<Task>();
        int threadCount = 10;
        for (int i = 0; i < threadCount; i++)
        {
            var i1 = i;
            actions.Add(async () =>
            {
                var localclient = await ImmuClient.NewBuilder()
                 .WithServerPort(3325)
                 .Open();
                clients.Push(localclient);
                await localclient.VerifiedSet("key" + i1, "val" + i1);
                Entry readEntry = await localclient.VerifiedGet("key" + i1);
                Assert.AreEqual("val" + i1, Encoding.UTF8.GetString(readEntry.Value));
                await localclient.Close();
            });
        }
        foreach (var action in actions)
        {
            tasks.Add(Task.Factory.StartNew(action));
        }
        try
        {
            await Task.WhenAll(tasks.ToArray());
        }
        catch (AggregateException e)
        {
            TestContext!.WriteLine("\nThe following exceptions have been thrown by WaitAll():");
            for (int j = 0; j < e.InnerExceptions.Count; j++)
            {
                TestContext!.WriteLine("\n-------------------------------------------------\n{0}", e.InnerExceptions[j].ToString());
            }
        }

        TestContext!.WriteLine("Done.");
    }
}