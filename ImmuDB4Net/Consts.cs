namespace ImmuDB;

/// <summary>
/// ImmuDB client specific constants
/// </summary>
public static class Consts
{

    // __________ prefixes __________

    /// <summary>
    /// HTree's byte prefix of a leaf's digest. 
    /// </summary>
    public static readonly byte LEAF_PREFIX = 0;

    /// <summary>
    /// HTree's byte prefix of a node (non-leaf)'s digest. 
    /// </summary>
    public static readonly byte NODE_PREFIX = 1;

    /// <summary>
    /// ZEntry's byte prefix for key
    /// </summary>
    public static readonly byte SET_KEY_PREFIX = 0;
    /// <summary>
    /// ZEntry's byte prefix for the encoded key
    /// </summary>
    public static readonly byte SORTED_SET_KEY_PREFIX = 1;

    /// <summary>
    /// Entry's value prefix in the digest
    /// </summary>
    public static readonly byte PLAIN_VALUE_PREFIX = 0;

    /// <summary>
    /// Entry's reference value prefix in the digest
    /// </summary>
    public static readonly byte REFERENCE_VALUE_PREFIX = 1;

    // __________ sizes & lengths __________

    /// <summary>
    /// The size (in bytes) of the data type used for storing the length of a SHA256 checksum. 
    /// </summary>
    public static readonly int SHA256_SIZE = 32;

    /// <summary>
    /// The size (in bytes) of the data type used for storing the transaction identifier.
    /// </summary>
    public static readonly int TX_ID_SIZE = 8;

    /// <summary>
    /// The size (in bytes) of the data type used for storing the transaction timestamp.
    /// </summary>
    public static readonly int TS_SIZE = 8;

    /// <summary>
    /// The size (in bytes) of the data type used for storing the sorted set length. 
    /// </summary>
    public static readonly int SET_LEN_LEN = 8;

    /// <summary>
    /// The size (in bytes) of the data type used for storing the score length. 
    /// </summary>
    public static readonly int SCORE_LEN = 8;

    /// <summary>
    /// The size (in bytes) of the data type used for storing the length of a key length.
    /// </summary>
    public static readonly int KEY_LEN_LEN = 8;
}