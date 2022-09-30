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
/// Represents an ImmuDB value, such as the value of a specific key
/// </summary>
public class Entry
{
    /// <summary>
    /// Gets the transaction ID
    /// </summary>
    /// <value></value>
    public ulong Tx { get; private set; }

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
    /// Gets the reference
    /// </summary>
    /// <value></value>
    public Reference? ReferencedBy { get; private set; }

    private Entry(byte[] key, byte[] value) {
        Key = key;
        Value = value;
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
    /// Converts from a gRPC entry
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public static Entry ValueOf(ImmudbProxy.Entry e)
    {
        ImmudbProxy.Entry proxyInst = e ?? ImmudbProxy.Entry.DefaultInstance;
        Entry entry = new Entry(proxyInst.Key.ToByteArray(), proxyInst.Value.ToByteArray());
        entry.Tx = proxyInst.Tx;

        if (proxyInst.Metadata != null)
        {
            entry.Metadata = KVMetadata.ValueOf(proxyInst.Metadata);
        }

        if (proxyInst.ReferencedBy != null)
        {
            entry.ReferencedBy = Reference.ValueOf(proxyInst.ReferencedBy);
        }

        return entry;
    }

    /// <summary>
    /// Gets the encoded key
    /// </summary>
    /// <returns></returns>
    public byte[] GetEncodedKey()
    {
        if (ReferencedBy == null)
        {
            return Utils.WrapWithPrefix(Key, Consts.SET_KEY_PREFIX);
        }

        return Utils.WrapWithPrefix(ReferencedBy.Key, Consts.SET_KEY_PREFIX);
    }

    /// <summary>
    /// Gets the digest for a version
    /// </summary>
    /// <param name="version">The version</param>
    /// <returns></returns>
    public byte[] DigestFor(int version)
    {
        KV kv;

        if (ReferencedBy == null)
        {
            kv = new KV(
                    GetEncodedKey(),
                    Metadata,
                    Utils.WrapWithPrefix(Value, Consts.PLAIN_VALUE_PREFIX)
            );
        }
        else
        {
            kv = new KV(
                    GetEncodedKey(),
                    ReferencedBy.Metadata,
                    Utils.WrapReferenceValueAt(Utils.WrapWithPrefix(Key, Consts.SET_KEY_PREFIX), ReferencedBy.AtTx)
            );
        }

        return kv.DigestFor(version);
    }

}
