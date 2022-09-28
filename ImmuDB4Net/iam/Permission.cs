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
/// The user permissions enumeration
/// </summary>
public enum Permission
{
    /// <summary>
    /// System Administrator permission
    /// </summary>
    PERMISSION_SYS_ADMIN = 255,
    /// <summary>
    /// Administrator permission
    /// </summary>
    PERMISSION_ADMIN = 254,
    /// <summary>
    /// None
    /// </summary>
    PERMISSION_NONE = 0,
    /// <summary>
    /// Read permission
    /// </summary>
    PERMISSION_R = 1,
    /// <summary>
    /// Read-Write permission
    /// </summary>
    PERMISSION_RW = 2
}