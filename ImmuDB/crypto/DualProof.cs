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

public class DualProof {

    public TxHeader SourceTxHeader {get; private set;}
    public TxHeader TargetTxHeader {get; private set;}
    public byte[][] InclusionProof {get; private set;}
    public byte[][] ConsistencyProof {get; private set;}
    public byte[] targetBlTxAlh {get; private set;}
    public byte[][] LastInclusionProof {get; private set;}
    public LinearProof LinearProof {get; private set;}

    public DualProof(TxHeader sourceTxHeader,
                     TxHeader targetTxHeader,
                     byte[][] inclusionProof,
                     byte[][] consistencyProof,
                     byte[] targetBlTxAlh,
                     byte[][] lastInclusionProof,
                     LinearProof linearProof) {
        this.SourceTxHeader = sourceTxHeader;
        this.TargetTxHeader = targetTxHeader;
        this.InclusionProof = inclusionProof;
        this.ConsistencyProof = consistencyProof;
        this.targetBlTxAlh = targetBlTxAlh;
        this.LastInclusionProof = lastInclusionProof;
        this.LinearProof = linearProof;
    }

    public static DualProof ValueOf(ImmudbProxy.DualProof proof) {
        return new DualProof(
                TxHeader.ValueOf(proof.SourceTxHeader),
                TxHeader.ValueOf(proof.TargetTxHeader),
                Utils.ConvertSha256ListToBytesArray(proof.InclusionProof),
                Utils.ConvertSha256ListToBytesArray(proof.ConsistencyProof),
                proof.TargetBlTxAlh.ToByteArray(),
                Utils.ConvertSha256ListToBytesArray(proof.LastInclusionProof),
                LinearProof.ValueOf(proof.LinearProof)
        );
    }
}