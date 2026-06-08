namespace AIKernel.Common.Logging;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Logging.LogUtil']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Logging.LogUtil']" />
public static class LogUtil
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Logging.LogUtil.Info']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Logging.LogUtil.Info']" />
    public static void Info(string message)
        => Write("INFO", message);

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Logging.LogUtil.Warn']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Logging.LogUtil.Warn']" />
    public static void Warn(string message)
        => Write("WARN", message);

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Logging.LogUtil.Error']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Logging.LogUtil.Error']" />
    public static void Error(string message)
        => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        Console.WriteLine($"[{level}] {message}");
    }
}
