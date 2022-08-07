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

/**
 * KV represents a key value pair.
 */
public class KV
{
    public byte[] Key { get; private set; }
    public KVMetadata? Metadata { get; private set; }
    public byte[] Value { get; private set; }

    public KV(byte[] key, KVMetadata? metadata, byte[] value)
    {
        this.Key = key;
        this.Metadata = metadata;
        this.Value = value;
    }

    public byte[] DigestFor(int version)
    {
        switch (version)
        {
            case 0: return Digest_v0();
            case 1: return Digest_v1();
        }

        throw new InvalidOperationException("unsupported tx header version");
    }

    byte[] Digest_v0()
    {
        if (Metadata != null)
        {
            throw new InvalidOperationException("metadata is unsupported when in 1.1 compatibility mode");
        }

        byte[] b = new byte[Key.Length + Consts.SHA256_SIZE];
        Array.Copy(Key, b, Key.Length);

        byte[] hvalue = CryptoUtils.Sha256Sum(Value);
        Array.Copy(Value, 0, b, Key.Length, hvalue.Length);

        return CryptoUtils.Sha256Sum(b);
    }

    byte[] Digest_v1()
    {
        byte[] mdbs = new byte[] { };
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
            Utils.WriteArray(bw, CryptoUtils.Sha256Sum(Value));
        }
        return CryptoUtils.Sha256Sum(bytes.ToArray());
    }
}