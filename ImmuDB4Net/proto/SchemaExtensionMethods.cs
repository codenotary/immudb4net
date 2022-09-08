using System.Diagnostics.CodeAnalysis;

namespace ImmudbProxy;

public sealed partial class Signature
{

    private static Signature? _defaultInstance;
    private static Object sync = new Object();

    public static Signature DefaultInstance
    {
        get
        {
            if (_defaultInstance == null)
            {
                lock (sync)
                {
                    _defaultInstance = new Signature();
                }
            }
            return _defaultInstance;
        }
    }
}

public sealed partial class Entry
{
    private static Entry? _defaultInstance;
    private static Object sync = new Object();
    public static Entry DefaultInstance
    {
        get
        {
            if (_defaultInstance == null)
            {
                lock (sync)
                {
                    _defaultInstance = new Entry();
                }
            }
            return _defaultInstance;
        }
    }
}

[ExcludeFromCodeCoverage]
public partial class ImmuService {

}

[ExcludeFromCodeCoverage]
public partial class ImmutableState {

}

[ExcludeFromCodeCoverage]
public partial class AuthConfig {

}

[ExcludeFromCodeCoverage]
public partial class ChangePasswordRequest {

}

[ExcludeFromCodeCoverage]
public partial class ChangePermissionRequest {

}

[ExcludeFromCodeCoverage]
public partial class Database {

}

[ExcludeFromCodeCoverage]
public partial class Chunk {

}

[ExcludeFromCodeCoverage]
public partial class Column {

}

[ExcludeFromCodeCoverage]
public partial class CommittedSQLTx {

}
