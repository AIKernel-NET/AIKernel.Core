namespace AIKernel.Common.Logging;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Logging.LogUtil']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Logging.LogUtil']/summary" />
public static class LogUtil
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Logging.LogUtil.Info']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Logging.LogUtil.Info']/summary" />
    public static void Info(string message)
        => Write("INFO", message);

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Logging.LogUtil.Warn']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Logging.LogUtil.Warn']/summary" />
    public static void Warn(string message)
        => Write("WARN", message);

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Logging.LogUtil.Error']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Logging.LogUtil.Error']/summary" />
    public static void Error(string message)
        => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        Console.WriteLine($"[{level}] {message}");
    }
}
