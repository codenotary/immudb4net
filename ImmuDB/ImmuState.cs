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
   
    private readonly String database;
    private readonly long txId;
    private readonly byte[] txHash;
    private readonly byte[] signature;

    public ImmuState(String database, long txId, byte[] txHash, byte[] signature)
    {
        this.database = database;
        this.txId = txId;
        this.txHash = txHash;
        this.signature = signature;
    }
    
    private bool CheckSignature(AsymmetricKeyParameter publicKey)
    {
        if (publicKey == null)
        {
            return true;
        }

        if (signature != null && signature.Length > 0)
        {
            var payload = toBytes();
            ISigner signer = SignerUtilities.GetSigner(HASH_ENCRYPTION_ALGORITHM);
            signer.Init(false, publicKey);
            signer.BlockUpdate(payload, 0, payload.Length);
            return signer.VerifySignature(signature);
        }

        return false;
    }

     private byte[] toBytes() {
        byte[] result = new byte[4 + database.Length + 8 + Consts.SHA256_SIZE];
        int i = 0;
        Utils.putUint32(database.Length, result, i);
        i += 4;
        var databaseBytes = Encoding.UTF8.GetBytes(database);
        Array.Copy(databaseBytes, 0, result, i, database.Length);
        i += database.Length;
        Utils.putUint64(txId, result, i);
        i += 8;
        Array.Copy(txHash, 0, result, i, txHash.Length);
        return result;
    }

    // The asymmetric key parameter.
    // Pem filename.
    internal static AsymmetricKeyParameter GetPublicKeyFromPemFile(string pemFilename) {
        StreamReader fileStream = System.IO.File.OpenText(pemFilename);
        PemReader pemReader = new PemReader(fileStream);
        AsymmetricKeyParameter keyParameter = (AsymmetricKeyParameter)pemReader.ReadObject();
        return keyParameter;
    }
}