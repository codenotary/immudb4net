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
        using (SHA256 sha256Hash = SHA256.Create()) {
            return sha256Hash.ComputeHash(data);
        }
    }
}