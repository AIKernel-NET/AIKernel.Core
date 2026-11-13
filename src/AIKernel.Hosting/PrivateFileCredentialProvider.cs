using System.Collections.Immutable;
using AIKernel.Abstractions.Security;

namespace AIKernel.Hosting;

/// <summary>Resolves only configured secret names from bounded private files outside the public directory.</summary>
public sealed class PrivateFileCredentialProvider : ISecureCredentialProvider
{
    private readonly ImmutableDictionary<string,string> paths;
    private readonly string publicRoot;
    /// <summary>File paths are trusted server configuration, never request parameters.</summary>
    public PrivateFileCredentialProvider(IReadOnlyDictionary<string,string> allowedFiles,string publicDirectory)
    {
        paths=allowedFiles.ToImmutableDictionary(p=>p.Key,p=>Path.GetFullPath(p.Value),StringComparer.Ordinal);
        publicRoot=Path.GetFullPath(publicDirectory).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
    }
    /// <inheritdoc />
    public async ValueTask<string> GetSecretAsync(string key,CancellationToken cancellationToken=default)
    {
        if(!paths.TryGetValue(key,out var path))throw new InvalidOperationException("Secret reference is not allowed.");
        var file=new FileInfo(path);
        if(file.LinkTarget is not null)throw new InvalidOperationException("Secret symlinks are not supported.");
        var comparison=OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal;
        if(path.StartsWith(publicRoot,comparison)||!file.Exists||file.Length is <1 or >4096)throw new InvalidOperationException("Private secret file is unavailable.");
        // Reject symlinked parents as well, so an apparent private path cannot resolve inside the web root.
        for(var dir=file.Directory;dir is not null;dir=dir.Parent)
            if(dir.LinkTarget is not null)throw new InvalidOperationException("Secret directory symlinks are not supported.");
        if(!OperatingSystem.IsWindows() && (File.GetUnixFileMode(path)&(UnixFileMode.GroupRead|UnixFileMode.GroupWrite|UnixFileMode.GroupExecute|
            UnixFileMode.OtherRead|UnixFileMode.OtherWrite|UnixFileMode.OtherExecute))!=0)throw new InvalidOperationException("Secret file permissions are too broad.");
        string value=(await File.ReadAllTextAsync(path,cancellationToken).ConfigureAwait(false)).Trim();
        if(value.Length is <16 or >1024||value.Any(char.IsWhiteSpace)||value.Any(char.IsControl))throw new InvalidOperationException("Invalid secret file.");
        return value;
    }
}
