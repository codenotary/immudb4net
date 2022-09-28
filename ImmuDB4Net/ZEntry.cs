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
/// Represents a (scored key : value) entry 
/// </summary>
public class ZEntry
{
    private static int setLenLen = 8;
    private static int scoreLen = 8;
    private static int keyLenLen = 8;
    private static int txIDLen = 8;

    /// <summary>
    /// Gets the set
    /// </summary>
    /// <value></value>
    public byte[] Set { get; private set; }

    /// <summary>
    /// Gets the key
    /// </summary>
    /// <value></value>
    public byte[] Key { get; private set; }

    /// <summary>
    /// Gets the <see cref="Entry" /> instance
    /// </summary>
    /// <value></value>
    public Entry Entry { get; private set; }

    /// <summary>
    /// Gets the score value
    /// </summary>
    /// <value></value>
    public double Score { get; private set; }


    internal ulong AtTx;

    private ZEntry(byte[] key, byte[] set, Entry entry)
    {
        Key = key;
        Set = set;
        Entry = entry;
    }

    /// <summary>
    /// Converts from a gRPC Protobuf ZEntry object
    /// </summary>
    /// <param name="e"></param>
    /// <returns></returns>
    public static ZEntry ValueOf(ImmudbProxy.ZEntry e)
    {
        ZEntry entry = new ZEntry(e.Key.ToByteArray(), e.Set.ToByteArray(), ImmuDB.Entry.ValueOf(e.Entry ?? ImmudbProxy.Entry.DefaultInstance));
        entry.Score = e.Score;
        entry.AtTx = e.AtTx;
        return entry;
    }

    /// <summary>
    /// Provides the instance's encoded content
    /// </summary>
    /// <returns></returns>
    public byte[] GetEncodedKey()
    {
        byte[] encodedKey = Utils.WrapWithPrefix(Key, Consts.SET_KEY_PREFIX);

        MemoryStream zKey = new MemoryStream(setLenLen + Set.Length + scoreLen + keyLenLen + encodedKey.Length + txIDLen);
        using (BinaryWriter bw = new BinaryWriter(zKey))
        {
            Utils.WriteWithBigEndian(bw, Set.Length);
            Utils.WriteArray(bw, Set);
            Utils.WriteWithBigEndian(bw, Score);
            Utils.WriteWithBigEndian(bw, encodedKey.Length);
            Utils.WriteArray(bw, encodedKey);
            Utils.WriteWithBigEndian(bw, AtTx);
        }

        return Utils.WrapWithPrefix(zKey.ToArray(), Consts.SORTED_SET_KEY_PREFIX);
    }

    /// <summary>
    /// Provides the digest for a specific version 
    /// </summary>
    /// <param name="version">The version</param>
    /// <returns></returns>
    public byte[] DigestFor(int version)
    {
        KV kv = new KV(
                    GetEncodedKey(),
                    null,
                    new byte[] { }
        );

        return kv.DigestFor(version);
    }
}