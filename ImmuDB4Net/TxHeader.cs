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
/// Stores the transaction's header information
/// </summary>
public class TxHeader
{
    /// <summary>
    /// Gets the version
    /// </summary>
    /// <value></value>
    public int Version { get; private set; }
    /// <summary>
    /// Gets the transaction id
    /// </summary>
    /// <value></value>
    public ulong Id { get; private set; }
    /// <summary>
    /// Gets the Previous Alh 
    /// </summary>
    /// <value></value>
    public byte[] PrevAlh { get; private set; }
    /// <summary>
    /// Gets the timestamp in epoch microseconds
    /// </summary>
    /// <value></value>
    public long Ts { get; private set; }
    /// <summary>
    /// Gets the count of transaction entries
    /// </summary>
    /// <value></value>
    public int NEntries { get; private set; }
    /// <summary>
    /// Gets the SHA256 hash value of the hash tree root
    /// </summary>
    /// <value></value>
    public byte[] Eh { get; private set; }
    /// <summary>
    /// Gets the Bl Transaction ID
    /// </summary>
    /// <value></value>
    public ulong BlTxId { get; private set; }
    /// <summary>
    /// Gets the Bl Root
    /// </summary>
    /// <value></value>
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

    /// <summary>
    /// Converts an gRPC TxHeader to ImmuClient TxHeader
    /// </summary>
    /// <param name="hdr"></param>
    /// <returns></returns>
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

    /// <summary>
    /// Gets the ALH hash code.
    /// </summary>
    /// <returns></returns>
    public byte[] Alh()
    {
        // txID + prevAlh + innerHash
        MemoryStream bytes = new MemoryStream(Consts.TX_ID_SIZE + 2 * Consts.SHA256_SIZE);
        using (BinaryWriter bw = new BinaryWriter(bytes))
        {
            Utils.WriteWithBigEndian(bw, Id);
            Utils.WriteArray(bw, PrevAlh);
            Utils.WriteArray(bw, InnerHash());
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
                        Utils.WriteWithBigEndian(bw, (short)NEntries);
                        break;
                    }
                case 1:
                    {
                        // TODO: add support for TxMetadata
                        Utils.WriteWithBigEndian(bw, (short)0L);
                        Utils.WriteWithBigEndian(bw, (uint)NEntries);
                        break;
                    }
                default:
                    {
                        throw new InvalidOperationException($"missing tx hash calculation method for version {Version}");
                    }
                    // following records are currently common in versions 0 and 1
            }
            Utils.WriteArray(bw, Eh);
            Utils.WriteWithBigEndian(bw, BlTxId);
            Utils.WriteArray(bw, BlRoot);
        }

        return CryptoUtils.Sha256Sum(bytes.ToArray());
    }
}