namespace AIKernel.Common.Logging;

/// <summary>[EN] Documents this public package API member. [JA] LogUtil を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Logging.LogUtil']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Logging.LogUtil']/summary" />
public static class LogUtil
{
    /// <summary>[EN] Documents this public package API member. [JA] Info を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Logging.LogUtil.Info']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Logging.LogUtil.Info']/summary" />
    public static void Info(string message)
        => Write("INFO", message);

    /// <summary>[EN] Documents this public package API member. [JA] Warn を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Logging.LogUtil.Warn']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Logging.LogUtil.Warn']/summary" />
    public static void Warn(string message)
        => Write("WARN", message);

    /// <summary>[EN] Documents this public package API member. [JA] Error を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Logging.LogUtil.Error']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Logging.LogUtil.Error']/summary" />
    public static void Error(string message)
        => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        Console.WriteLine($"[{level}] {message}");
    }
}
