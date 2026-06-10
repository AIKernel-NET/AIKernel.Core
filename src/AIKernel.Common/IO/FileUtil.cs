namespace AIKernel.Common.IO;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.IO.FileUtil']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.IO.FileUtil']/summary" />
public static class FileUtil
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.IO.FileUtil.ReadTextAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.IO.FileUtil.ReadTextAsync']/summary" />
    public static async Task<string> ReadTextAsync(string path)
    {
        return await File.ReadAllTextAsync(path);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.IO.FileUtil.WriteTextAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.IO.FileUtil.WriteTextAsync']/summary" />
    public static async Task WriteTextAsync(string path, string content)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(path, content);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.IO.FileUtil.Exists']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.IO.FileUtil.Exists']/summary" />
    public static bool Exists(string path)
        => File.Exists(path);
}
