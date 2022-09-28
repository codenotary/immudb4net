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

namespace ImmuDB.Iam;

/// <summary>
/// Represents User specific fields
/// </summary>
public class User
{
    /// <summary>
    /// Gets the name
    /// </summary>
    /// <value></value>
    public string Name { get; private set; }
    /// <summary>
    /// Gets or sets the created date
    /// </summary>
    /// <value></value>
    public string? CreatedAt { get; set; }
    /// <summary>
    /// Gets or sets the created by value
    /// </summary>
    /// <value></value>
    public string? CreatedBy { get; set; }
    /// <summary>
    /// Gets or sets the active flag
    /// </summary>
    /// <value></value>
    public bool Active { get; set; }
    /// <summary>
    /// Gets the permission list
    /// </summary>
    /// <value></value>
    public List<Permission> Permissions { get; private set; }

    /// <summary>
    /// Creates a <see cref="User" /> instance
    /// </summary>
    /// <param name="name">The username</param>
    public User(string name)
    {
        Name = name;
        Permissions = new List<Permission>();
    }

    /// <summary>
    /// Creates a <see cref="User" /> instance
    /// </summary>
    /// <param name="name">The username</param>
    /// <param name="permissions">The permission list</param>
    public User(string name, List<Permission> permissions)
    {
        Name = name;
        Permissions = new List<Permission>(permissions);
    }

    /// <summary>
    /// Creates a string representation of the <see cref="User" /> instance
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        // if I use string interpolation it looks uglier to escape the combination of { and '
        return string.Format("User{user='{0}', createdAt='{1}', createdBy='{2}', active={3}, permissions={4}", Name, CreatedAt, CreatedBy, Active, Permissions);
    }
}