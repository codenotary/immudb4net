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

using Grpc.Core;
using ImmuDB.Iam;

namespace ImmuDB.Tests;

[TestClass]
public class UserMgmtTests : BaseClientIntTests
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

    [TestMethod("create user and change password")]
    public async Task Test1()
    {
        await client!.Open("immudb", "immudb", "defaultdb");

        try
        {
            await client.CreateUser("testUser", "testTest123!", Permission.PERMISSION_ADMIN, "defaultdb");
        }
        catch (Exception)
        {
            Assert.Fail("Exception in create user");
        }

        await client.ChangePassword("testUser", "testTest123!", "newTestTest123!");
        await client.Close();

        // This must fail.
        try
        {
            await client.Open("testUser", "testTest123!", "defauldb");
            Assert.Fail("Login with wrong (old) password must fail.");
        }
        catch (RpcException)
        {
            // Login failed, everything's fine.
        }

        await client.Open("testUser", "newTestTest123!", "defaultdb");
        await client.Close();

        // Some basic test to temporary (until t1 test above can be used) increase the code coverage.
        User myUser = new User("myusername") {
            CreatedAt = "sometimestamp",
            CreatedBy = "me",
            Active = true,
            Permissions = {Permission.PERMISSION_R}
        };
        
        Assert.AreEqual(myUser.Name, "myusername", "Usernames are different");
        Assert.AreEqual(myUser.CreatedAt, "sometimestamp", "CreatedAt values are different");
        Assert.AreEqual(myUser.CreatedBy, "me", "CreatedBy values are different");
        Assert.IsTrue(myUser.Active, "User is not active, as expected");
        CollectionAssert.AreEqual(myUser.Permissions, new List<Permission>() {Permission.PERMISSION_R}, "Permissions are different");
    }
}