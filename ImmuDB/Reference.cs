namespace ImmuDB;

public class Reference
{
    public ulong Tx { get; private set; }
    public byte[] Key { get; private set; }
    public ulong AtTx { get; private set; }
    public KVMetadata? Metadata { get; private set; }

    private Reference(byte[] key) {
        Key = key;
    }

    public static Reference ValueOf(ImmudbProxy.Reference proxyRef)
    {
        Reference reference = new Reference(proxyRef.Key.ToByteArray());
        reference.Tx = proxyRef.Tx;
        reference.AtTx = proxyRef.AtTx;

        if (proxyRef.Metadata != null)
        {
            reference.Metadata = KVMetadata.ValueOf(proxyRef.Metadata);
        }

        return reference;
    }
}