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

namespace ImmuDB;

using System;
using System.Text;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Security;

public class ImmuState
{
    private const string HASH_ENCRYPTION_ALGORITHM = "SHA256withECDSA";

    public string Database { get; set; }
    public ulong TxId { get; set; }
    public byte[] TxHash { get; set; }
    public byte[] Signature { get; set; }

    public ImmuState(string database, ulong txId, byte[] txHash, byte[] signature)
    {
        this.Database = database;
        this.TxId = txId;
        this.TxHash = txHash;
        this.Signature = signature;
    }

    public bool CheckSignature(AsymmetricKeyParameter? publicKey)
    {
        if (publicKey == null)
        {
            return true;
        }

        if (Signature != null && Signature.Length > 0)
        {
            var payload = ToBytes();
            ISigner signer = SignerUtilities.GetSigner(HASH_ENCRYPTION_ALGORITHM);
            signer.Init(false, publicKey);
            signer.BlockUpdate(payload, 0, payload.Length);
            return signer.VerifySignature(Signature);
        }

        return false;
    }

    private byte[] ToBytes()
    {
        byte[] result = new byte[4 + Database.Length + 8 + Consts.SHA256_SIZE];
        int i = 0;
        Utils.PutUint32(Database.Length, result, i);
        i += 4;
        var databaseBytes = Encoding.UTF8.GetBytes(Database);
        Array.Copy(databaseBytes, 0, result, i, Database.Length);
        i += Database.Length;
        Utils.PutUint64(TxId, result, i);
        i += 8;
        Array.Copy(TxHash, 0, result, i, TxHash.Length);
        return result;
    }

    // The asymmetric key parameter.
    // Pem filename.
    internal static AsymmetricKeyParameter GetPublicKeyFromPemFile(string pemFilename)
    {
        StreamReader fileStream = System.IO.File.OpenText(pemFilename);
        PemReader pemReader = new PemReader(fileStream);
        AsymmetricKeyParameter keyParameter = (AsymmetricKeyParameter)pemReader.ReadObject();
        return keyParameter;
    }

    // This method converts Proto ImmutableState to ImmuState
    internal static ImmuState ValueOf(ImmudbProxy.ImmutableState state) {
        
        return new ImmuState(
                state.Db,
                state.TxId,
                state.TxHash.ToByteArray(),
                (state.Signature ?? new ImmudbProxy.Signature()).Signature_.ToByteArray()
        );
    }
}