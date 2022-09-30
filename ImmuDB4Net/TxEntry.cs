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

using ImmuDB.Crypto;

namespace ImmuDB;

/// <summary>
/// Represents A transaction entry that belongs to a <see cref="Tx" /> class
/// </summary>
public class TxEntry
{
    /// <summary>
    /// Gets the key
    /// </summary>
    /// <value></value>
    public byte[] Key {get; private set;}
    /// <summary>
    /// The transaction metadata
    /// </summary>
    /// <value></value>
    public KVMetadata Metadata {get; private set;}
    /// <summary>
    /// The VLength parameter
    /// </summary>
    /// <value></value>
    public int VLength {get; private set;}
    /// <summary>
    /// The hash value
    /// </summary>
    /// <value></value>
    public byte[] HVal {get; private set;}

    private TxEntry(byte[] key, KVMetadata metadata, int vLength, byte[] hVal)
    {
        this.Key = new byte[key.Length];
        Array.Copy(key, 0, this.Key, 0, key.Length);

        this.Metadata = metadata;
        this.VLength = vLength;
        this.HVal = hVal;
    }

    /// <summary>
    /// Converts from a gRPC protobuf TxEntry instance
    /// </summary>
    /// <param name="txe"></param>
    /// <returns></returns>
    public static TxEntry ValueOf(ImmudbProxy.TxEntry txe)
    {
        KVMetadata md = new KVMetadata();

        if (txe.Metadata != null)
        {
            md = KVMetadata.ValueOf(txe.Metadata);
        }

        return new TxEntry(
                        txe.Key.ToByteArray(),
                        md,
                        txe.VLen,
                        CryptoUtils.DigestFrom(txe.HValue.ToByteArray())
                );
    }

    /// <summary>
    /// Calculates the digest for a specific version
    /// </summary>
    /// <param name="version">The version number</param>
    /// <returns></returns>
    public byte[] DigestFor(int version)
    {
        switch (version)
        {
            case 0: return Digest_v0();
            case 1: return Digest_v1();
        }

        throw new InvalidOperationException("unsupported tx header version");
    }

    /// <summary>
    /// Calculates the digest for version 0
    /// </summary>
    /// <returns></returns>
    public byte[] Digest_v0()
    {
        if (Metadata != null)
        {
            throw new InvalidOperationException("metadata is unsupported when in 1.1 compatibility mode");
        }

        byte[] b = new byte[Key.Length + Consts.SHA256_SIZE];

        Array.Copy(Key, 0, b, 0, Key.Length);
        Array.Copy(HVal, 0, b, Key.Length, HVal.Length);

        return CryptoUtils.Sha256Sum(b);
    }

    /// <summary>
    /// Calculates the digest for version 1
    /// </summary>
    /// <returns></returns>
    public byte[] Digest_v1()
    {
        byte[] mdbs = new byte[0];
        int mdLen = 0;

        if (Metadata != null)
        {
            mdbs = Metadata.Serialize();
            mdLen = mdbs.Length;
        }

        MemoryStream bytes = new MemoryStream(2 + mdLen + 2 + Key.Length + Consts.SHA256_SIZE);
        using (BinaryWriter bw = new BinaryWriter(bytes)) 
        {
            Utils.WriteWithBigEndian(bw, (short)mdLen);
            if (mdLen > 0)
            {
                Utils.WriteArray(bw, mdbs);
            }
            Utils.WriteWithBigEndian(bw, (short)Key.Length);
            Utils.WriteArray(bw, Key);
            Utils.WriteArray(bw, HVal);

        }
        return CryptoUtils.Sha256Sum(bytes.ToArray());
    }

}