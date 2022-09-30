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

/// <summary>
/// Represents the database state data
/// </summary>
public class ImmuState
{
    private const string HASH_ENCRYPTION_ALGORITHM = "SHA256withECDSA";

    /// <summary>
    /// Gets or sets the database name
    /// </summary>
    /// <value></value>
    public string Database { get; set; }
    /// <summary>
    /// Gets or sets the Transaction ID
    /// </summary>
    /// <value></value>
    public ulong TxId { get; set; }
    /// <summary>
    /// Gets or sets transaction hash
    /// </summary>
    /// <value></value>
    public byte[] TxHash { get; set; }
    /// <summary>
    /// Gets or sets the signature array. This can be checked by using <see cref="CheckSignature" /> function
    /// </summary>
    /// <value></value>
    public byte[] Signature { get; set; }

    /// <summary>
    /// Creates an ImmuState object
    /// </summary>
    /// <param name="database">The database name</param>
    /// <param name="txId">The transaction ID</param>
    /// <param name="txHash">The transaction hash</param>
    /// <param name="signature">The signature</param>
    public ImmuState(string database, ulong txId, byte[] txHash, byte[] signature)
    {
        this.Database = database;
        this.TxId = txId;
        this.TxHash = txHash;
        this.Signature = signature;
    }

    /// <summary>
    /// Checks the signature
    /// </summary>
    /// <param name="publicKey">The public/private key</param>
    /// <returns>True if the check succeeded</returns>
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
                (state.Signature ?? ImmudbProxy.Signature.DefaultInstance).Signature_.ToByteArray()
        );
    }
}