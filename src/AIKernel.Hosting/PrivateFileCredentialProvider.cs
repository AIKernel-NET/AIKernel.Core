using System.Collections.Immutable;
using AIKernel.Abstractions.Security;

namespace AIKernel.Hosting;

/// <summary>Resolves only configured secret names from bounded private files outside the public directory.</summary>
public sealed class PrivateFileCredentialProvider : ISecureCredentialProvider
{
    private readonly ImmutableDictionary<string, string> paths;
    private readonly string publicRoot;

    public PrivateFileCredentialProvider(IReadOnlyDictionary<string, string> allowedFiles, string publicDirectory)
    {
        paths = allowedFiles.ToImmutableDictionary(pair => pair.Key, pair => Path.GetFullPath(pair.Value), StringComparer.Ordinal);
        publicRoot = Path.GetFullPath(publicDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
    }

    public async ValueTask<string> GetSecretAsync(string key, CancellationToken cancellationToken = default)
    {
        if (!paths.TryGetValue(key, out var path))
            throw new InvalidOperationException("Secret reference is not allowed.");

        var file = new FileInfo(path);
        if (file.LinkTarget is not null)
            throw new InvalidOperationException("Secret symlinks are not supported.");

        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (path.StartsWith(publicRoot, comparison) || !file.Exists || file.Length is < 1 or > 4096)
            throw new InvalidOperationException("Private secret file is unavailable.");

        for (var directory = file.Directory; directory is not null; directory = directory.Parent)
        {
            if (directory.LinkTarget is not null)
                throw new InvalidOperationException("Secret directory symlinks are not supported.");
        }

        const UnixFileMode publicAccess = UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute;
        if (!OperatingSystem.IsWindows() && (File.GetUnixFileMode(path) & publicAccess) != 0)
            throw new InvalidOperationException("Secret file permissions are too broad.");

        var value = (await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false)).Trim();
        if (value.Length is < 16 or > 1024 || value.Any(char.IsWhiteSpace) || value.Any(char.IsControl))
            throw new InvalidOperationException("Invalid secret file.");
        return value;
    }
}
