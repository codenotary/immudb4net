using System.Text;
using Google.Protobuf;

namespace ImmuDB;

public static class Utils 
{
     public static ByteString toByteString(String str) {
        if (str == null) {
            return ByteString.Empty;
        }

        return ByteString.CopyFrom(str, Encoding.UTF8);
    }

    public static ByteString toByteString(byte[] b) {
        if (b == null) {
            return ByteString.Empty;
        }

        return ByteString.CopyFrom(b);
    }

    public static byte[] toByteArray(String str) {
        if (str == null) {
            return new byte[] {};
        }

        return Encoding.UTF8.GetBytes(str);
    }

    public static byte[] wrapWithPrefix(byte[] b, byte prefix) {
        if (b == null) {
            return new byte[] {};
        }
        byte[] wb = new byte[b.Length + 1];
        wb[0] = prefix;
        Array.Copy(b, 0, wb, 1, b.Length);
        return wb;
    }

    public static byte[] wrapReferenceValueAt(byte[] key, long atTx) {
        byte[] refVal = new byte[1 + 8 + key.Length];
        refVal[0] = Consts.REFERENCE_VALUE_PREFIX;

        Utils.putUint64(atTx, refVal, 1);

        Array.Copy(key, 0, refVal, 1 + 8, key.Length);
        return refVal;
    }

    /**
     * Convert the list of SHA256 (32-length) bytes to a primitive byte[][].
     */
    public static byte[][] convertSha256ListToBytesArray(List<ByteString> data) {
        if (data == null) {
            return new byte[][] {};
        }
        int size = data.Count;
        byte[][] result = new byte[size][];
        for (int i = 0; i < size; i++) {
            byte[] item = data[i].ToByteArray();
            result[i] = new byte[32];
            Array.Copy(item, result[i], Math.Max(32, item.Length));
        }
        return result;
    }

    public static void putUint32(int value, byte[] dest, int destPos) {
        // Considering gRPC's generated code that maps Go's uint32 and int32 to C#'s int,
        // this is basically the version of this Go code:
        // binary.BigEndian.PutUint32(target[targetIdx:], value)
        var valueBytes = BitConverter.GetBytes(value);
        if(BitConverter.IsLittleEndian) {
            Array.Reverse(valueBytes);
        }        
        Array.Copy(valueBytes, 0, dest, destPos, valueBytes.Length);
    }

    public static void putUint64(long value, byte[] dest) {
        putUint64(value, dest, 0);
    }

    public static void putUint64(long value, byte[] dest, int destPos) {
        // Considering gRPC's generated code that maps Go's uint64 and int64 to Java's long,
        // this is basically the Java version of this Go code:
        // binary.BigEndian.PutUint64(target[targetIdx:], value)
        var valueBytes = BitConverter.GetBytes(value);
        if(BitConverter.IsLittleEndian) {
            Array.Reverse(valueBytes);
        }        
        Array.Copy(valueBytes, 0, dest, destPos, valueBytes.Length);
    }
}