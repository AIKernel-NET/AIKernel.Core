namespace AIKernel.Core.Vfs.Abstractions;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Vfs.Abstractions.VfsPathRules']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Vfs.Abstractions.VfsPathRules']" />
public static class VfsPathRules
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Abstractions.VfsPathRules.Normalize']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Abstractions.VfsPathRules.Normalize']" />
    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("Path is required.", nameof(path));
        }

        var normalized = path.Replace('\\', '/').Trim();

        if (normalized is "/" or ".")
        {
            return string.Empty;
        }

        var segments = normalized
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var result = new List<string>();

        foreach (var segment in segments)
        {
            if (segment == ".")
            {
                continue;
            }

            if (segment == "..")
            {
                // Side effect: none.
                // Fail-closed path traversal rejection.
                throw new ArgumentException("Path traversal is not allowed.", nameof(path));
            }

            result.Add(segment);
        }

        return string.Join('/', result);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Abstractions.VfsPathRules.GetName']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Abstractions.VfsPathRules.GetName']" />
    public static string GetName(string path)
    {
        var normalized = Normalize(path);

        if (normalized.Length == 0)
        {
            return "/";
        }

        var index = normalized.LastIndexOf('/');
        return index < 0 ? normalized : normalized[(index + 1)..];
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Abstractions.VfsPathRules.IsUnder']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Abstractions.VfsPathRules.IsUnder']" />
    public static bool IsUnder(string parent, string child)
    {
        parent = Normalize(parent);
        child = Normalize(child);

        return parent.Length == 0
            || child.Equals(parent, StringComparison.Ordinal)
            || child.StartsWith(parent + "/", StringComparison.Ordinal);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Abstractions.VfsPathRules.IsDirectChild']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Abstractions.VfsPathRules.IsDirectChild']" />
    public static bool IsDirectChild(string parent, string child)
    {
        parent = Normalize(parent);
        child = Normalize(child);

        var relative = parent.Length == 0
            ? child
            : child.StartsWith(parent + "/", StringComparison.Ordinal)
                ? child[(parent.Length + 1)..]
                : string.Empty;

        return relative.Length > 0 && !relative.Contains('/', StringComparison.Ordinal);
    }
}