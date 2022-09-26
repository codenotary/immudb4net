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
/// Represents a transaction
/// </summary>
public class Tx
{
    /// <summary>
    /// Gets the transaction header
    /// </summary>
    /// <value></value>
    public TxHeader Header {get; private set;}
    /// <summary>
    /// Get the transaction entries
    /// </summary>
    /// <value></value>
    public List<TxEntry> Entries {get; private set;}
    /// <summary>
    /// Gets the hash tree of the transaction
    /// </summary>
    /// <value></value>
    public HTree Htree {get; private set;}

    private Tx(TxHeader header, List<TxEntry> entries, HTree htree)
    {
        this.Header = header;
        this.Entries = entries;
        this.Htree = htree;
    }

    /// <summary>
    /// Converts from a gRPC protobuf Tx instance
    /// </summary>
    /// <param name="stx"></param>
    /// <returns></returns>
    public static Tx ValueOf(ImmudbProxy.Tx stx)
    {
        TxHeader header = TxHeader.ValueOf(stx.Header);

        List<TxEntry> entries = new List<TxEntry>(stx.Entries.Count);

        stx.Entries.ToList().ForEach(txe => { entries.Add(TxEntry.ValueOf(txe)); });

        HTree hTree = new HTree(entries.Count);

        Tx tx = new Tx(header, entries, hTree);

        tx.BuildHashTree();

        if (!tx.Header.Eh.SequenceEqual(hTree.Root()))
        {
            throw new InvalidOperationException("corrupted data, eh doesn't match expected value");
        }

        return tx;
    }

    /// <summary>
    /// Builds the hash tree from the entries
    /// </summary>
    public void BuildHashTree()
    {
        byte[][] digests = new byte[Entries.Count][];
        for (int i = 0; i < Entries.Count; i++)
        {
            digests[i] = Entries[i].DigestFor(Header.Version);
        }
        Htree.BuildWith(digests);
    }

    /// <summary>
    /// Computes the inclusion proof
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public InclusionProof Proof(byte[] key)
    {
        int kindex = IndexOf(key);
        if (kindex < 0)
        {
            throw new KeyNotFoundException();
        }
        return Htree.InclusionProof(kindex);
    }

    private int IndexOf(byte[] key)
    {
        for (int i = 0; i < Entries.Count; i++)
        {
            if (Entries[i].Key.SequenceEqual(key))
            {
                return i;
            }
        }
        return -1;
    }

}
