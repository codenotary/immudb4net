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

public class User
{
    public string Name { get; private set; }
    public string? CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public bool Active { get; set; }
    public List<Permission> Permissions { get; private set; }

    public User(string name)
    {
        Name = name;
        Permissions = new List<Permission>();
    }
    
    public User(string name, List<Permission> permissions)
    {
        Name = name;
        Permissions = new List<Permission>(permissions);
    }

    public override string ToString()
    {
        // if I use string interpolation it looks uglier to escape the combination of { and '
        return string.Format("User{user='%s', createdAt='%s', createdBy='%s', active=%s, permissions=%s}", Name, CreatedAt, CreatedBy, Active, Permissions);
    }
}