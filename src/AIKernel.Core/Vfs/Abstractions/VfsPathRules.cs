namespace AIKernel.Core.Vfs.Abstractions;

using AIKernel.Common.Results;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Vfs.Abstractions.VfsPathRules']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Vfs.Abstractions.VfsPathRules']/summary" />
public static class VfsPathRules
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Abstractions.VfsPathRules.Normalize']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Abstractions.VfsPathRules.Normalize']/summary" />
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

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Abstractions.VfsPathRules.GetName']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Abstractions.VfsPathRules.GetName']/summary" />
    public static string GetName(string path)
    {
        var normalized = Normalize(path);

        if (normalized.Length == 0)
        {
            return "/";
        }

        var index = normalized.LastIndexOf('/');
        return NameFromIndex(normalized, index);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Abstractions.VfsPathRules.IsUnder']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Abstractions.VfsPathRules.IsUnder']/summary" />
    public static bool IsUnder(string parent, string child)
    {
        parent = Normalize(parent);
        child = Normalize(child);

        return parent.Length == 0
            || child.Equals(parent, StringComparison.Ordinal)
            || child.StartsWith(parent + "/", StringComparison.Ordinal);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Abstractions.VfsPathRules.IsDirectChild']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Vfs.Abstractions.VfsPathRules.IsDirectChild']/summary" />
    public static bool IsDirectChild(string parent, string child)
    {
        parent = Normalize(parent);
        child = Normalize(child);

        var relative = RelativeChild(parent, child);

        return relative.Length > 0 && !relative.Contains('/', StringComparison.Ordinal);
    }

    private static string NameFromIndex(string normalized, int index)
        => NameIndexDecision(normalized, index).Match(value => value, value => value);

    private static Either<string, string> NameIndexDecision(string normalized, int index)
        => index < 0
            ? Either<string, string>.FromLeft(normalized)
            : Either<string, string>.FromRight(normalized[(index + 1)..]);

    private static string RelativeChild(string parent, string child)
        => ParentDecision(parent)
            .Match(
                _ => child,
                nonRootParent => ChildUnderParent(nonRootParent, child).Match(() => string.Empty, value => value));

    private static Either<string, string> ParentDecision(string parent)
        => parent.Length == 0
            ? Either<string, string>.FromLeft(parent)
            : Either<string, string>.FromRight(parent);

    private static Option<string> ChildUnderParent(string parent, string child)
        => child.StartsWith(parent + "/", StringComparison.Ordinal)
            ? Option<string>.Some(child[(parent.Length + 1)..])
            : Option<string>.None();
}
