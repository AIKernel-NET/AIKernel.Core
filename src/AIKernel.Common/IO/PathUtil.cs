namespace AIKernel.Common.IO;

/// <summary>EN: Documentation for public API. JA: PathUtil を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.IO.PathUtil']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.IO.PathUtil']/summary" />
public static class PathUtil
{
    /// <summary>EN: Documentation for public API. JA: Normalize を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.IO.PathUtil.Normalize']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.IO.PathUtil.Normalize']/summary" />
    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        // Expand "~" for Unix-like environments
        if (path.StartsWith("~"))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            path = Path.Combine(home, path.Substring(1));
        }

        return Path.GetFullPath(path);
    }

    /// <summary>EN: Documentation for public API. JA: Combine を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.IO.PathUtil.Combine']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.IO.PathUtil.Combine']/summary" />
    public static string Combine(params string[] parts)
        => Path.Combine(parts);
}
