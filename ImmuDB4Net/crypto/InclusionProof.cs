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

namespace ImmuDB.Crypto;

/// <summary>
/// Represents an inclusion proof
/// </summary>
public class InclusionProof
{
    /// <summary>
    /// Gets the Leaf ID
    /// </summary>
    /// <value></value>
    public int Leaf {get; private set;}
    /// <summary>
    /// Gets the width of the inclusion proof
    /// </summary>
    /// <value></value>
    public int Width {get; private set;}
    /// <summary>
    /// Gets the terms
    /// </summary>
    /// <value></value>
    public byte[][] Terms {get; private set;}

    /// <summary>
    /// Constructs an inclusion proof
    /// </summary>
    /// <param name="leaf">Leaf ID</param>
    /// <param name="width">The width</param>
    /// <param name="terms">The terms</param>
    public InclusionProof(int leaf, int width, byte[][] terms) {
        this.Leaf = leaf;
        this.Width = width;
        this.Terms = terms;
    }

    /// <summary>
    /// Converts from a gRPC InclusionProof
    /// </summary>
    /// <param name="proof"></param>
    /// <returns>The InclusionProof</returns>
    public static InclusionProof ValueOf(ImmudbProxy.InclusionProof proof) {
        return new InclusionProof(proof.Leaf, proof.Width, CryptoUtils.DigestsFrom(proof.Terms));
    }
}
