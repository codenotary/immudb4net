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

public class TxHeader
{
    public int Version { get; private set; }
    public ulong Id { get; private set; }
    public byte[] PrevAlh { get; private set; }
    public long Ts { get; private set; }
    public int NEntries { get; private set; }
    public byte[] Eh { get; private set; }
    public ulong BlTxId { get; private set; }
    public byte[] BlRoot { get; private set; }

    private static readonly int TS_SIZE = 8;
    private static readonly int SHORT_SSIZE = 2;
    private static readonly int LONG_SSIZE = 4;

    private static readonly int maxTxMetadataLen = 0;

    private TxHeader(int version, ulong id, byte[] prevAlh, long ts, int nEntries,
                  byte[] eh, ulong blTxId, byte[] blRoot)
    {
        this.Version = version;
        this.Id = id;
        this.PrevAlh = prevAlh;
        this.Ts = ts;
        this.NEntries = nEntries;
        this.Eh = eh;
        this.BlTxId = blTxId;
        this.BlRoot = blRoot;
    }

    public static TxHeader ValueOf(ImmudbProxy.TxHeader hdr)
    {
        return new TxHeader(
                hdr.Version,
                hdr.Id,
                hdr.PrevAlh.ToByteArray(),
                hdr.Ts,
                hdr.Nentries,
                hdr.EH.ToByteArray(),
                hdr.BlTxId,
                hdr.BlRoot.ToByteArray()
        );
    }

    public byte[] Alh()
    {
        // txID + prevAlh + innerHash
        MemoryStream bytes = new MemoryStream(Consts.TX_ID_SIZE + 2 * Consts.SHA256_SIZE);
        using (BinaryWriter bw = new BinaryWriter(bytes))
        {
            Utils.WriteWithBigEndian(bw, Id);
            Utils.WriteWithBigEndian(bw, PrevAlh);
            Utils.WriteWithBigEndian(bw, InnerHash());
        }

        return CryptoUtils.Sha256Sum(bytes.ToArray());
    }

    private byte[] InnerHash()
    {
        // ts + version + (mdLen + md)? + nentries + eH + blTxID + blRoot
        MemoryStream bytes = new MemoryStream(TS_SIZE +
                SHORT_SSIZE + (SHORT_SSIZE + maxTxMetadataLen) +
                LONG_SSIZE + Consts.SHA256_SIZE +
                Consts.TX_ID_SIZE + Consts.SHA256_SIZE);

        using (BinaryWriter bw = new BinaryWriter(bytes))
        {
            Utils.WriteWithBigEndian(bw, Ts);
            Utils.WriteWithBigEndian(bw, (short)Version);

            switch (Version)
            {
                case 0:
                    {
                        Utils.WriteWithBigEndian(bw, NEntries);
                        break;
                    }
                case 1:
                    {
                        // TODO: add support for TxMetadata
                        Utils.WriteWithBigEndian(bw, (short)0);
                        Utils.WriteWithBigEndian(bw, NEntries);
                        break;
                    }
                default:
                    {
                        throw new InvalidOperationException($"missing tx hash calculation method for version {Version}");
                    }
                    // following records are currently common in versions 0 and 1
            }
            Utils.WriteWithBigEndian(bw, Eh);
            Utils.WriteWithBigEndian(bw, BlTxId);
            Utils.WriteWithBigEndian(bw, BlRoot);
        }

        return CryptoUtils.Sha256Sum(bytes.ToArray());
    }
}