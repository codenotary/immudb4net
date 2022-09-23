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

namespace ImmuDB;

public static class Utils
{
    public static ByteString ToByteString(string str)
    {
        if (str == null)
        {
            return ByteString.Empty;
        }

        return ByteString.CopyFrom(str, Encoding.UTF8);
    }

    public static ByteString ToByteString(byte[] b)
    {
        if (b == null)
        {
            return ByteString.Empty;
        }

        return ByteString.CopyFrom(b);
    }

    public static byte[] ToByteArray(string str)
    {
        if (str == null)
        {
            return new byte[] { };
        }

        return Encoding.UTF8.GetBytes(str);
    }

    public static byte[] WrapWithPrefix(byte[] b, byte prefix)
    {
        if (b == null)
        {
            return new byte[] { };
        }
        byte[] wb = new byte[b.Length + 1];
        wb[0] = prefix;
        Array.Copy(b, 0, wb, 1, b.Length);
        return wb;
    }

    public static byte[] WrapReferenceValueAt(byte[] key, ulong atTx)
    {
        byte[] refVal = new byte[1 + 8 + key.Length];
        refVal[0] = Consts.REFERENCE_VALUE_PREFIX;

        Utils.PutUint64(atTx, refVal, 1);

        Array.Copy(key, 0, refVal, 1 + 8, key.Length);
        return refVal;
    }
    
    /// <summary>
    /// Convert the list of SHA256 (32-length) bytes to a primitive byte[][].
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    public static byte[][] ConvertSha256ListToBytesArray(RepeatedField<ByteString> data)
    {
        if (data == null)
        {
            return new byte[][] { };
        }
        int size = data.Count;
        byte[][] result = new byte[size][];
        for (int i = 0; i < size; i++)
        {
            byte[] item = data[i].ToByteArray();
            result[i] = new byte[32];
            Array.Copy(item, result[i], Math.Max(32, item.Length));
        }
        return result;
    }

    public static void PutUint32(int value, byte[] dest, int destPos)
    {
        // Considering gRPC's generated code that maps Go's uint32 and int32 to C#'s int,
        // this is basically the version of this Go code:
        // binary.BigEndian.PutUint32(target[targetIdx:], value)
        var valueBytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(valueBytes);
        }
        Array.Copy(valueBytes, 0, dest, destPos, valueBytes.Length);
    }

    public static void PutUint64(ulong value, byte[] dest)
    {
        PutUint64(value, dest, 0);
    }

    public static void PutUint64(ulong value, byte[] dest, int destPos)
    {
        // Considering gRPC's generated code that maps Go's uint64 and int64 to Java's long,
        // this is basically the Java version of this Go code:
        // binary.BigEndian.PutUint64(target[targetIdx:], value)
        var valueBytes = BitConverter.GetBytes(value);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(valueBytes);
        }
        Array.Copy(valueBytes, 0, dest, destPos, valueBytes.Length);
    }

    public static void WriteLittleEndian(BinaryWriter bw, ulong item)
    {
        var content = BitConverter.GetBytes(item);
        if (!BitConverter.IsLittleEndian)
        {
            Array.Reverse(content);
        }
        bw.Write(content);
    }

    public static void WriteWithBigEndian(BinaryWriter bw, ulong item)
    {
        var content = BitConverter.GetBytes(item);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(content);
        }
        bw.Write(content);
    }

    public static void WriteWithBigEndian(BinaryWriter bw, uint item)
    {
        var content = BitConverter.GetBytes(item);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(content);
        }
        bw.Write(content);
    }

    public static void WriteWithBigEndian(BinaryWriter bw, ushort item)
    {
        var content = BitConverter.GetBytes(item);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(content);
        }
        bw.Write(content);
    }

    public static void WriteWithBigEndian(BinaryWriter bw, long item)
    {
        var content = BitConverter.GetBytes(item);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(content);
        }
        bw.Write(content);
    }

    public static void WriteWithBigEndian(BinaryWriter bw, double item)
    {
        var content = BitConverter.GetBytes(item);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(content);
        }
        bw.Write(content);
    }

    public static void WriteWithBigEndian(BinaryWriter bw, short item)
    {
        var content = BitConverter.GetBytes(item);
        if (BitConverter.IsLittleEndian)
        {
            Array.Reverse(content);
        }
        bw.Write(content);
    }

    public static void WriteArray(BinaryWriter bw, byte[]? item)
    {
        if ((item == null) || (item.Length == 0))
        {
            return;
        }
        bw.Write(item);
    }

    public static string GenerateShortHash(string source)
    {
        using (SHA256 sha256 = SHA256.Create())
        {
            var result = sha256.ComputeHash(Encoding.UTF8.GetBytes(source));
            var hash = Convert.ToBase64String(result).ToUpper()
                .Replace("=", "")
                .Replace("+", "-")
                .Replace("/", "_")
                .Substring(0, 30);
            return hash;
        }
    }

}