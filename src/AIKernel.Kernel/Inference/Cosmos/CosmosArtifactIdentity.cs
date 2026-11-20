using System.Buffers;
using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;

namespace AIKernel.Kernel.Inference;

/// <summary>Verified import provenance. Digest consistency alone does not authenticate a publisher.</summary>
public sealed record CosmosArtifactIdentity(string Repository,string Revision,string ManifestSha256,bool TrustedManifestPinned);

/// <summary>Concrete Cosmos dependency selection, not arbitrary semantic pruning of dense transformer layers.</summary>
public sealed record CosmosMemoryPlan(ImmutableArray<string> Modules,int ContextTokens,long SelectedWeightBytes,long LogicalKvBytes,
    long MaximumResidentBytes,long ProtectedResidentBytes,string Storage);

internal static class CosmosArtifactVerifier
{
    internal static CosmosArtifactIdentity Verify(string directory,string family,string? expectedDigest,int workers = 1)
        => Inspect(directory, family, expectedDigest, verifyContents: true, workers);

    internal static bool IsAvailable(string directory,string family,string expectedDigest)
    {
        try
        {
            Inspect(directory, family, expectedDigest, verifyContents: false);
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException
            or JsonException or InvalidOperationException or KeyNotFoundException) { return false; }
    }

    private static CosmosArtifactIdentity Inspect(string directory,string family,string? expectedDigest,bool verifyContents,int workers = 1)
    {
        string path=Path.Combine(directory,"import-manifest.json");
        if(!File.Exists(path)||new FileInfo(path).Length>1_048_576)throw new InvalidDataException("A bounded import manifest is required for Cosmos paging.");
        byte[] bytes=File.ReadAllBytes(path);string digest=Convert.ToHexStringLower(SHA256.HashData(bytes));
        if(expectedDigest is not null && (expectedDigest.Length!=64||!string.Equals(digest,expectedDigest,StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Cosmos import manifest does not match the trusted digest.");
        using var doc=JsonDocument.Parse(bytes);var root=doc.RootElement;
        string repository=root.GetProperty("repository").GetString()!,revision=root.GetProperty("revision").GetString()!;
        string required=family switch{"cosmos3_edge"=>"nvidia/Cosmos3-Edge","cosmos3_omni"=>"nvidia/Cosmos3-Nano",_=>"nvidia/Cosmos-Reason2-2B"};
        if(repository!=required||revision is not { Length:40 }||revision.Any(c=>!Uri.IsHexDigit(c)))throw new InvalidDataException("Unexpected Cosmos import lineage.");
        if(!root.TryGetProperty("metadataSha256",out var metadata)||metadata.ValueKind!=JsonValueKind.Object)
            throw new InvalidDataException("Cosmos metadata digests are required.");
        var requiredMetadata=new HashSet<string>(["config.json","tokenizer.json","preprocessor_config.json","video_preprocessor_config.json",
            family=="cosmos3_edge"?"chat_template.jinja":"chat_template.json"],StringComparer.Ordinal);
        var allowedMetadata=new HashSet<string>(["config.json","tokenizer.json","tokenizer_config.json","preprocessor_config.json",
            "video_preprocessor_config.json","processor_config.json","chat_template.json","chat_template.jinja"],StringComparer.Ordinal);
        foreach(var entry in metadata.EnumerateObject())
        {
            if(!allowedMetadata.Remove(entry.Name)||entry.Value.ValueKind!=JsonValueKind.String)
                throw new InvalidDataException("Invalid or duplicate Cosmos metadata entry.");
            string metadataPath=Path.Combine(directory,entry.Name);
            if(new FileInfo(metadataPath).Length>128L*1024*1024)throw new InvalidDataException("Cosmos metadata exceeds the file budget.");
            CheckDigest(metadataPath,entry.Value.GetString(),verifyContents);
            requiredMetadata.Remove(entry.Name);
        }
        if(requiredMetadata.Count!=0)throw new InvalidDataException("Incomplete Cosmos metadata manifest.");
        string indexPath=Path.Combine(directory,"model.safetensors.index.json");
        if(new FileInfo(indexPath).Length>16_777_216)throw new InvalidDataException("Shard index exceeds 16 MiB.");
        using var index=JsonDocument.Parse(File.ReadAllText(indexPath));
        var names=index.RootElement.GetProperty("weight_map").EnumerateObject().Select(x=>x.Value.GetString()!).ToHashSet(StringComparer.Ordinal);
        var shards=root.GetProperty("shards");if(shards.GetArrayLength() is <1 or >32)throw new InvalidDataException("Invalid shard manifest.");
        var files = new List<(string Path, string Digest)>();
        foreach(var item in shards.EnumerateArray())
        {
            string name=item.GetProperty("output").GetString()!,hash=item.GetProperty("outputSha256").GetString()!;
            if(string.IsNullOrWhiteSpace(name)||name!=Path.GetFileName(name)||name.IndexOfAny(['/', '\\', ':'])>=0||!name.EndsWith(".safetensors",StringComparison.Ordinal)||!names.Remove(name))
                throw new InvalidDataException("Invalid or duplicate manifest shard.");
            files.Add((Path.Combine(directory,name),hash));
        }
        if(names.Count!=0)throw new InvalidDataException("Incomplete Cosmos shard manifest.");
        void CheckFiles(int first, int stride)
        {
            for (int i = first; i < files.Count; i += stride)
                CheckDigest(files[i].Path, files[i].Digest, verifyContents, large: true);
        }
        if (verifyContents && workers >= 2 && files.Count >= 2 && OperatingSystem.IsWindows())
        {
            // Hash independent shards concurrently, bounded to two sequential readers.
            var second = Task.Run(() => CheckFiles(1, 2));
            try { CheckFiles(0, 2); }
            finally { second.GetAwaiter().GetResult(); }
        }
        else CheckFiles(0, 1);
        return new(repository,revision,digest,expectedDigest is not null);
    }

    private static void CheckDigest(string path,string? expected,bool verifyContents,bool large = false)
    {
        if (expected is not { Length:64 } || !expected.All(Uri.IsHexDigit))
            throw new InvalidDataException("Invalid digest for Cosmos file: "+Path.GetFileName(path));
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.SequentialScan);
        if (file.Length==0 || (verifyContents &&
            !string.Equals(Convert.ToHexStringLower(large ? HashShard(file) : SHA256.HashData(file)),expected,StringComparison.OrdinalIgnoreCase)))
            throw new InvalidDataException("Empty Cosmos file or digest mismatch: "+Path.GetFileName(path));
    }

    private static byte[] HashShard(Stream file)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(1024 * 1024);
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            int read;
            while ((read = file.Read(buffer)) != 0) hash.AppendData(buffer, 0, read);
            return hash.GetHashAndReset();
        }
        finally { ArrayPool<byte>.Shared.Return(buffer, clearArray: true); }
    }
}
