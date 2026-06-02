namespace AIKernel.Core.Vfs.Abstractions;

public static class VfsPathRules
{
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

    public static bool IsUnder(string parent, string child)
    {
        parent = Normalize(parent);
        child = Normalize(child);

        return parent.Length == 0
            || child.Equals(parent, StringComparison.Ordinal)
            || child.StartsWith(parent + "/", StringComparison.Ordinal);
    }

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