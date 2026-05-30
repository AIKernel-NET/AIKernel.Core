namespace AIKernel.Common.IO;

public static class PathUtil
{
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

    public static string Combine(params string[] parts)
        => Path.Combine(parts);
}
