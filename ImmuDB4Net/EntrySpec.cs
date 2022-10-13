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

namespace ImmuDB;

/// <summary>
/// Represents an ImmuDB Entry value without reference, such as the value of a specific key, plus metadata
/// </summary>
public class EntrySpec
{
    /// <summary>
    /// Gets the key
    /// </summary>
    /// <value></value>
    public byte[] Key { get; private set; }

    /// <summary>
    /// Gets the value
    /// </summary>
    /// <value></value>
    public byte[] Value { get; private set; }

    /// <summary>
    /// Gets the metadata
    /// </summary>
    /// <value></value>
    public KVMetadata? Metadata { get; private set; }

    /// <summary>
    /// Creates an EntrySpec instance
    /// </summary>
    /// <param name="key">The key</param>
    /// <param name="metadata">The metadata</param>
    /// <param name="referenceKey">The reference</param>
    /// <param name="atTx">Transaction ID</param>
    public EntrySpec(byte[] key, KVMetadata? metadata, byte[] referenceKey, ulong atTx) {
        Key = key;
        Metadata = metadata;
        Value = Utils.WrapReferenceValueAt(Utils.WrapWithPrefix(referenceKey, Consts.SET_KEY_PREFIX), atTx);
    }

    /// <summary>
    /// Gets the string representation of the value
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        if((Value == null) || (Value.Length == 0)) {
            return "";
        }
        return Encoding.UTF8.GetString(Value);
    }

    

    /// <summary>
    /// Gets the encoded key
    /// </summary>
    /// <returns></returns>
    public byte[] GetEncodedKey()
    {
        return Utils.WrapWithPrefix(Key, Consts.SET_KEY_PREFIX);
    }

    /// <summary>
    /// Gets the digest for a version
    /// </summary>
    /// <param name="version">The version</param>
    /// <returns></returns>
    public byte[] DigestFor(int version)
    {
        KV kv;
        kv = new KV(
                Utils.WrapWithPrefix(Key, Consts.SET_KEY_PREFIX),
                Metadata,
                Value
        );
        return kv.DigestFor(version);
    }

}
