namespace AIKernel.Common.IO;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.IO.PathUtil']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.IO.PathUtil']" />
public static class PathUtil
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.IO.PathUtil.Normalize']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.IO.PathUtil.Normalize']" />
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

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.IO.PathUtil.Combine']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.IO.PathUtil.Combine']" />
    public static string Combine(params string[] parts)
        => Path.Combine(parts);
}
