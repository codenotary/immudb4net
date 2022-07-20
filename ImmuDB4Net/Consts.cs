namespace ImmuDB;

public static class Consts {

    // __________ prefixes __________

    /**
     * HTree's byte prefix of a leaf's digest.
     */
    public static readonly byte LEAF_PREFIX = 0;

    /**
     * HTree's byte prefix of a node (non-leaf)'s digest.
     */
    public static readonly byte NODE_PREFIX = 1;

    public static readonly byte SET_KEY_PREFIX = 0;
    public static readonly byte SORTED_SET_KEY_PREFIX = 1;

    public static readonly byte PLAIN_VALUE_PREFIX = 0;
    public static readonly byte REFERENCE_VALUE_PREFIX = 1;

    // __________ sizes & lengths __________

    /**
     * The size (in bytes) of the data type used for storing the length of a SHA256 checksum.
     */
    public static readonly int SHA256_SIZE = 32;

    /**
     * The size (in bytes) of the data type used for storing the transaction identifier.
     */
    public static readonly int TX_ID_SIZE = 8;

    /**
     * The size (in bytes) of the data type used for storing the transaction timestamp.
     */
    public static readonly int TS_SIZE = 8;

    /**
     * The size (in bytes) of the data type used for storing the sorted set length.
     */
    public static readonly int SET_LEN_LEN = 8;

    /**
     * The size (in bytes) of the data type used for storing the score length.
     */
    public static readonly int SCORE_LEN = 8;

    /**
     * The size (in bytes) of the data type used for storing the length of a key length.
     */
    public static readonly int KEY_LEN_LEN = 8;
}