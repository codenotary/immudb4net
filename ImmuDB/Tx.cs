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

public class Tx
{
    private TxHeader header;
    private List<TxEntry> entries;
    private HTree htree;

    private Tx(TxHeader header, List<TxEntry> entries, HTree htree)
    {
        this.header = header;
        this.entries = entries;
        this.htree = htree;
    }

    public static Tx ValueOf(ImmudbProxy.Tx stx)
    {
        TxHeader header = TxHeader.ValueOf(stx.Header);

        List<TxEntry> entries = new List<TxEntry>(stx.Entries.Count);

        stx.Entries.ToList().ForEach(txe => { entries.Add(TxEntry.valueOf(txe)); });

        HTree hTree = new HTree(entries.Count);

        Tx tx = new Tx(header, entries, hTree);

        tx.BuildHashTree();

        if (!tx.header.Eh.SequenceEqual(hTree.Root()))
        {
            throw new InvalidOperationException("corrupted data, eh doesn't match expected value");
        }

        return tx;
    }


    public void BuildHashTree()
    {
        byte[][] digests = new byte[entries.Count][];
        for (int i = 0; i < entries.Count; i++)
        {
            digests[i] = entries[i].DigestFor(header.Version);
        }
        htree.BuildWith(digests);
    }

    public InclusionProof Proof(byte[] key)
    {
        int kindex = IndexOf(key);
        if (kindex < 0)
        {
            throw new KeyNotFoundException();
        }
        return htree.inclusionProof(kindex);
    }

    private int IndexOf(byte[] key)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].Key.SequenceEqual(key))
            {
                return i;
            }
        }
        return -1;
    }

}
