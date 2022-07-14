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

using Org.BouncyCastle.Math;

namespace ImmuDB.Crypto;

/**
 * This is a hash tree implementation.
 * It is closely based on the Go version that is part of immudb 0.9 Go SDK.
 *
 * @author dxps
 */
public class HTree
{
    private byte[][][] levels;
    private int maxWidth;
    private int width;
    private byte[] root;

    public HTree(int maxWidth)
    {
        if (maxWidth < 1)
        {
            throw new ArgumentException("maxWidth must be greater or equal to 1");
        }

        this.maxWidth = maxWidth;

        int lw = 1;
        while (lw < maxWidth)
        {
            lw = lw << 1;
        }

        int height = BigInteger.ValueOf(maxWidth - 1).BitLength + 1;

        levels = new byte[height][][];

        for (int l = 0; l < height; l++)
        {
            levels[l] = new byte[lw >> l][];
        }
    }

    public void BuildWith(byte[][] digests)
    {

        if (digests == null || digests.Length == 0)
        {
            throw new ArgumentException(
                    "Provided digests must be non-null and have a length greater than 0.");
        }

        if (digests.Length > maxWidth)
        {
            throw new ArgumentException(
                    $"Provided digests' length of ${digests.Length} is bigger than tree's maxWidth of ${maxWidth}.");
        }

        for (int i = 0; i < digests.Length; i++)
        {
            byte[] leaf = new byte[33]; // 33 = 32 (sha256.Size) + 1
            leaf[0] = Consts.LEAF_PREFIX;
            Array.Copy(digests[i], 0, leaf, 1, digests[i].Length);
            levels[0][i] = CryptoUtils.Sha256Sum(leaf);
        }

        int l = 0;
        int w = digests.Length;

        while (w > 1)
        {
            byte[] b = new byte[65]; // 65 = 2 x 32 (sha256.Size) + 1
            b[0] = Consts.NODE_PREFIX;

            int wn = 0;

            for (int i = 0; i + 1 < w; i += 2)
            {
                Array.Copy(levels[l][i], 0, b, 1, levels[l][i].Length);
                Array.Copy(levels[l][i + 1], 0, b, 33, levels[l][i].Length);
                levels[l + 1][wn] = CryptoUtils.Sha256Sum(b);
                wn++;
            }

            if (w % 2 == 1)
            {
                levels[l + 1][wn] = levels[l][w - 1];
                wn++;
            }

            l++;
            w = wn;
        }

        width = digests.Length;
        root = levels[l][0];
    }

    /**
     * Get the root of the tree.
     *
     * @return A 32-long array of bytes.
     * @throws IllegalStateException when internal state (width) is zero.
     */
    public byte[] Root()
    {
        if (width == 0)
        {
            throw new InvalidOperationException();
        }
        return root;
    }

    /**
     * InclusionProof returns the shortest list of additional nodes required to
     * compute the root. It's an adaption of the algorithm for proof construction
     * that exists at github.com/codenotary/merkletree.
     *
     * @param i Index of the node from which the inclusion proof will be provided.
     */
    public InclusionProof inclusionProof(int i)
    {
        if (i >= width)
        {
            throw new ArgumentException($"Provided index (${i}) is higher then the tree's width (${width}).");
        }
        int m = i;
        int n = width;
        int offset = 0;
        int l, r;

        if (width == 1)
        {
            return new InclusionProof(i, width, null);
        }

        byte[][] terms = new byte[0][];
        while (true)
        {
            int d = countBits(n - 1);
            int k = 1 << (d - 1);

            if (m < k)
            {
                l = offset + k;
                r = offset + n - 1;
                n = k;
            }
            else
            {
                l = offset;
                r = offset + k - 1;
                m = m - k;
                n = n - k;
                offset += k;
            }
            int layer = countBits(r - l);
            int index = l / (1 << layer);

            byte[][] newterms = new byte[1 + terms.Length][];

            newterms[0] = levels[layer][index];
            Array.Copy(terms, 0, newterms, 1, terms.Length);
            terms = newterms;

            if (n < 1 || (n == 1 && m == 0))
            {
                return new InclusionProof(i, width, terms);
            }
        }
    }

    private static int countBits(int number)
    {
        if (number == 0)
        {
            return 0;
        }
        // log function in base 2 & take only integer part.
        return (int)(Math.Log(number) / Math.Log(2) + 1);
    }

}