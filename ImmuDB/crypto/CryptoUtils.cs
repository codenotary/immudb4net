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

using System.Security.Cryptography;
using System.Text;
using Google.Protobuf;
using Google.Protobuf.Collections;

namespace ImmuDB.Crypto;

public static class CryptoUtils
{
    // FYI: Interesting enough, Go returns a fixed value for sha256.Sum256(nil) and this value is:
    // [227 176 196 66 152 252 28 20 154 251 244 200 153 111 185 36 39 174 65 228 100 155 147 76 164 149 153 27 120 82 184 85]
    // whose Base64 encoded value is 47DEQpj8HBSa+/TImW+5JCeuQeRkm5NMpJWZG3hSuFU=.
    // So we treat this case as in Go.
    private static byte[] SHA256_SUM_OF_NULL = Convert.FromBase64String("47DEQpj8HBSa+/TImW+5JCeuQeRkm5NMpJWZG3hSuFU=");

    /**
     * This method returns a SHA256 digest of the provided data.
     */
    public static byte[] Sha256Sum(byte[] data)
    {
        if (data == null)
        {
            return SHA256_SUM_OF_NULL;
        }
        using (SHA256 sha256Hash = SHA256.Create())
        {
            return sha256Hash.ComputeHash(data);
        }
    }

    public static byte[][] DigestsFrom(RepeatedField<ByteString> terms)
    {
        if (terms == null)
        {
            return new byte[][] { };
        }
        int size = terms.Count;
        byte[][] result = new byte[size][];
        for (int i = 0; i < size; i++)
        {
            // the statements below could have easily been replaced with : results[i] = terms[i].ToByteArray(), but the solution below 
            // limits the size of each item in the jagged array
            result[i] = new byte[Consts.SHA256_SIZE];
            byte[] term = terms[i].ToByteArray();
            Array.Copy(term, 0, result[i], 0, Consts.SHA256_SIZE);
        }
        return result;
    }

    /**
    * Copy the provided `digest` array into a byte[32] array.
    */
    public static byte[] DigestFrom(byte[] digest)
    {
        if (digest.Length != Consts.SHA256_SIZE)
        {
            return new byte[] { };
        }
        byte[] d = new byte[Consts.SHA256_SIZE];
        Array.Copy(digest, 0, d, 0, Consts.SHA256_SIZE);
        return d;
    }

    public static bool VerifyInclusion(byte[][] iProof, ulong i, ulong j, byte[] iLeaf,
        byte[] jRoot)
    {
        if (i > j || i == 0 || (i < j && iProof.Length == 0))
        {
            return false;
        }
        byte[] ciRoot = evalInclusion(iProof, i, j, iLeaf);
        return jRoot.SequenceEqual(ciRoot);
    }

    private static byte[] evalInclusion(byte[][] iProof, ulong i, ulong j, byte[] iLeaf)
    {
        ulong i1 = i - 1;
        ulong j1 = j - 1;
        byte[] ciRoot = iLeaf;

        byte[] b = new byte[1 + Consts.SHA256_SIZE * 2];
        b[0] = Consts.NODE_PREFIX;

        foreach (byte[] h in iProof)
        {
            if (i1 % 2 == 0 && i1 != j1)
            {
                Array.Copy(ciRoot, 0, b, 1, ciRoot.Length);
                Array.Copy(h, 0, b, Consts.SHA256_SIZE + 1, h.Length);
            }
            else
            {
                Array.Copy(h, 0, b, 1, h.Length);
                Array.Copy(ciRoot, 0, b, Consts.SHA256_SIZE + 1, ciRoot.Length);
            }

            ciRoot = Sha256Sum(b);
            i1 >>= 1;
            j1 >>= 1;
        }

        return ciRoot;
    }

    public static bool VerifyInclusion(InclusionProof proof, byte[] digest, byte[] root)
    {
        if (proof == null)
        {
            return false;
        }

        byte[] leaf = new byte[1 + Consts.SHA256_SIZE];
        leaf[0] = Consts.LEAF_PREFIX;
        Array.Copy(digest, 0, leaf, 1, digest.Length);
        byte[] calcRoot = Sha256Sum(leaf);
        int i = proof.Leaf;
        int r = proof.Width - 1;

        if (proof.Terms != null)
        {
            for (int j = 0; j < proof.Terms.Length; j++)
            {
                byte[] b = new byte[1 + 2 * Consts.SHA256_SIZE];
                b[0] = Consts.NODE_PREFIX;

                if (i % 2 == 0 && i != r)
                {
                    Array.Copy(calcRoot, 0, b, 1, calcRoot.Length);
                    Array.Copy(proof.Terms[j], 0, b, 1 + Consts.SHA256_SIZE, proof.Terms[j].Length);
                }
                else
                {
                    Array.Copy(proof.Terms[j], 0, b, 1, proof.Terms[j].Length);
                    Array.Copy(calcRoot, 0, b, 1 + Consts.SHA256_SIZE, calcRoot.Length);
                }

                calcRoot = Sha256Sum(b);
                i /= 2;
                r /= 2;
            }
        }

        return i == r && root.SequenceEqual(calcRoot);
    }

    public static bool VerifyDualProof(DualProof proof, ulong sourceTxId, ulong targetTxId,
            byte[] sourceAlh, byte[] targetAlh)
    {

        if (proof == null || proof.SourceTxHeader == null || proof.TargetTxHeader == null
                || proof.SourceTxHeader.Id != sourceTxId
                || proof.TargetTxHeader.Id != targetTxId)
        {
            return false;
        }

        if (proof.SourceTxHeader.Id == 0
                || proof.SourceTxHeader.Id > proof.TargetTxHeader.Id)
        {
            return false;
        }

        if (!sourceAlh.SequenceEqual(proof.SourceTxHeader.Alh())
                || !targetAlh.SequenceEqual(proof.TargetTxHeader.Alh()))
        {
            return false;
        }

        if (sourceTxId < proof.TargetTxHeader.BlTxId)
        {
            if (!CryptoUtils.VerifyInclusion(proof.InclusionProof, sourceTxId,
                    proof.TargetTxHeader.BlTxId, leafFor(sourceAlh),
                    proof.TargetTxHeader.BlRoot))
            {
                return false;
            }
        }

        if (proof.SourceTxHeader.BlTxId > 0)
        {
            if (!CryptoUtils.VerifyConsistency(proof.ConsistencyProof,
                    proof.SourceTxHeader.BlTxId, proof.TargetTxHeader.BlTxId,
                    proof.SourceTxHeader.BlRoot, proof.TargetTxHeader.BlRoot))
            {
                return false;
            }
        }

        if (proof.TargetTxHeader.BlTxId > 0)
        {
            if (!VerifyLastInclusion(proof.LastInclusionProof, proof.TargetTxHeader.BlTxId,
                    leafFor(proof.targetBlTxAlh), proof.TargetTxHeader.BlRoot))
            {
                return false;
            }
        }

        if (sourceTxId < proof.TargetTxHeader.BlTxId)
        {
            return VerifyLinearProof(proof.LinearProof, proof.TargetTxHeader.BlTxId,
                    targetTxId, proof.targetBlTxAlh, targetAlh);
        }

        return VerifyLinearProof(proof.LinearProof, sourceTxId, targetTxId, sourceAlh, targetAlh);
    }

    private static byte[] leafFor(byte[] d)
    {
        byte[] b = new byte[1 + Consts.SHA256_SIZE];
        b[0] = Consts.LEAF_PREFIX;
        Array.Copy(d, 0, b, 1, d.Length);
        return Sha256Sum(b);
    }

    private static bool VerifyLinearProof(LinearProof proof, ulong sourceTxId, ulong targetTxId,
           byte[] sourceAlh, byte[] targetAlh)
    {

        if (proof == null || proof.SourceTxId != sourceTxId || proof.TargetTxId != targetTxId)
        {
            return false;
        }
        if (proof.SourceTxId == 0 || proof.SourceTxId > proof.TargetTxId || proof.Terms.Length == 0
                || !sourceAlh.SequenceEqual(proof.Terms[0]))
        {
            return false;
        }
        if (proof.Terms.Length != (int)(targetTxId - sourceTxId + 1))
        {
            return false;
        }
        byte[] calculatedAlh = proof.Terms[0];

        for (int i = 1; i < proof.Terms.Length; i++)
        {
            byte[] bs = new byte[Consts.TX_ID_SIZE + 2 * Consts.SHA256_SIZE];
            Utils.PutUint64(proof.SourceTxId + (ulong)i, bs);
            Array.Copy(calculatedAlh, 0, bs, Consts.TX_ID_SIZE, calculatedAlh.Length);
            Array.Copy(proof.Terms[i], 0, bs, Consts.TX_ID_SIZE + Consts.SHA256_SIZE, proof.Terms[i].Length);
            calculatedAlh = Sha256Sum(bs);
        }

        return targetAlh.SequenceEqual(calculatedAlh);
    }

    public static bool VerifyLastInclusion(byte[][] iProof, ulong i, byte[] leaf, byte[] root) {
        if (i == 0) {
            return false;
        }
        return root.SequenceEqual(EvalLastInclusion(iProof, i, leaf));
    }

    private static byte[] EvalLastInclusion(byte[][] iProof, ulong i, byte[] leaf) {
        ulong i1 = i - 1;
        byte[] root = leaf;

        byte[] b = new byte[1 + Consts.SHA256_SIZE * 2];
        b[0] = Consts.NODE_PREFIX;

        foreach (byte[] h in iProof) {
            Array.Copy(h, 0, b, 1, h.Length);
            Array.Copy(root, 0, b, Consts.SHA256_SIZE + 1, root.Length);
            root = Sha256Sum(b);
            i1 >>= 1;
        }
        return root;
    }

    public static bool VerifyConsistency(byte[][] cProof, ulong i, ulong j, byte[] iRoot,
            byte[] jRoot) {
        if (i > j || i == 0 || (i < j && cProof.Length == 0)) {
            return false;
        }

        if (i == j && cProof.Length == 0) {
            return iRoot.SequenceEqual(jRoot);
        }

        byte[][] result = EvalConsistency(cProof, i, j);
        byte[] ciRoot = result[0];
        byte[] cjRoot = result[1];

        return iRoot.SequenceEqual(ciRoot) && jRoot.SequenceEqual(cjRoot);
    }

    // Returns a "pair" (two) byte[] values (ciRoot, cjRoot), that's why
    // the returned data is byte[][] just to keep it simple.
    public static byte[][] EvalConsistency(byte[][] cProof, ulong i, ulong j) {

        ulong fn = i - 1;
        ulong sn = j - 1;

        while (fn % 2 == 1) {
            fn >>= 1;
            sn >>= 1;
        }

        byte[] ciRoot = cProof[0];
        byte[] cjRoot = cProof[0];

        byte[] b = new byte[1 + Consts.SHA256_SIZE * 2];
        b[0] = Consts.NODE_PREFIX;

        for (int k = 1; k < cProof.Length; k++) {
            byte[] h = cProof[k];
            if (fn % 2 == 1 || fn == sn) {
                Array.Copy(h, 0, b, 1, h.Length);

                Array.Copy(ciRoot, 0, b, 1 + Consts.SHA256_SIZE, ciRoot.Length);
                ciRoot = Sha256Sum(b);

                Array.Copy(cjRoot, 0, b, 1 + Consts.SHA256_SIZE, cjRoot.Length);
                cjRoot = Sha256Sum(b);

                while (fn % 2 == 0 && fn != 0) {
                    fn >>= 1;
                    sn >>= 1;
                }
            } else {
                Array.Copy(cjRoot, 0, b, 1, cjRoot.Length);
                Array.Copy(h, 0, b, 1 + Consts.SHA256_SIZE, h.Length);
                cjRoot = Sha256Sum(b);
            }
            fn >>= 1;
            sn >>= 1;
        }

        byte[][] result = new byte[2][];
        result[0] = ciRoot;
        result[1] = cjRoot;
        return result;
    }
}