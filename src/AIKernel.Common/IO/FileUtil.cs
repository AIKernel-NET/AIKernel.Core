namespace AIKernel.Common.IO;

/// <summary>[EN] Documents this public package API member. [JA] FileUtil を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.IO.FileUtil']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.IO.FileUtil']/summary" />
public static class FileUtil
{
    /// <summary>[EN] Documents this public package API member. [JA] ReadTextAsync を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.IO.FileUtil.ReadTextAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.IO.FileUtil.ReadTextAsync']/summary" />
    public static async Task<string> ReadTextAsync(string path)
    {
        return await File.ReadAllTextAsync(path);
    }

    /// <summary>[EN] Documents this public package API member. [JA] WriteTextAsync を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.IO.FileUtil.WriteTextAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.IO.FileUtil.WriteTextAsync']/summary" />
    public static async Task WriteTextAsync(string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(path, content);
    }

    /// <summary>[EN] Documents this public package API member. [JA] Exists を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.IO.FileUtil.Exists']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.IO.FileUtil.Exists']/summary" />
    public static bool Exists(string path)
        => File.Exists(path);
}
