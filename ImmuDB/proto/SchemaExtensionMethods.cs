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