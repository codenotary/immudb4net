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

namespace ImmuDB;

/// <summary>
/// Represents a key-value pair
/// </summary>
public class KVPair
{
    /// <summary>
    /// The key
    /// </summary>
    /// <value></value>
    public byte[] Key { get; private set; }
    /// <summary>
    /// The value
    /// </summary>
    /// <value></value>
    public byte[] Value { get; private set; }

    /// <summary>
    /// Creates a new key value pair
    /// </summary>
    /// <param name="key">The key</param>
    /// <param name="value">The value</param>
    /// <returns></returns>
    public KVPair(string key, byte[] value) :
        this(Utils.ToByteArray(key), value)
    {
    }

    /// <summary>
    /// Creates a new key value pair
    /// </summary>
    /// <param name="key">The key</param>
    /// <param name="value">The value</param>
    public KVPair(byte[] key, byte[] value)
    {
        this.Key = key;
        this.Value = value;
    }
}