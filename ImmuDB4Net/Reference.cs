namespace ImmuDB;

/// <summary>
/// Represents the reference data fields
/// </summary>
public class Reference
{
    /// <summary>
    /// The transaction ID
    /// </summary>
    /// <value></value>
    public ulong Tx { get; private set; }
    /// <summary>
    /// The key entry in a database
    /// </summary>
    /// <value></value>
    public byte[] Key { get; private set; }
    /// <summary>
    /// The transaction it refers to
    /// </summary>
    /// <value></value>
    public ulong AtTx { get; private set; }
    /// <summary>
    /// Gets the associated metadata
    /// </summary>
    /// <value></value>
    public KVMetadata? Metadata { get; private set; }

    private Reference(byte[] key) {
        Key = key;
    }

    /// <summary>
    /// Converts from a gRPC protobuf Reference object
    /// </summary>
    /// <param name="proxyRef"></param>
    /// <returns></returns>
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