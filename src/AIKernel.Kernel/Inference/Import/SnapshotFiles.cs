using System.Security.Cryptography;
using System.Text.Json;

namespace AIKernel.Kernel.Inference;

/// <summary>Owns the same read-only handles for digest checks and all subsequent reads.</summary>
internal sealed class SnapshotFiles : IDisposable
{
    private readonly Dictionary<string, FileStream> streams = new(StringComparer.Ordinal);
    internal FileStream this[string key] => streams[key];
    internal IEnumerable<string> Names => streams.Keys;
    internal string Primary { get; }
    private SnapshotFiles(string primary) => Primary = primary;

    internal static SnapshotFiles Open(string path, DenseRegistrationDocument registration)
    {
        bool onnx = registration.ReaderId == "onnx";
        string primary = onnx ? "onnx/model.onnx" : Path.GetFileName(path);
        string directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
        string root = onnx ? Directory.GetParent(directory)!.FullName : directory;
        var files = new SnapshotFiles(primary);
        try
        {
            files.VerifySnapshot(root, path, registration);
            return files;
        }
        catch { files.Dispose(); throw; }
    }

    private void VerifySnapshot(string root, string path, DenseRegistrationDocument registration)
    {
        using var spec = ResourceJson(registration.ReaderId);
        var artifacts = spec.RootElement.GetProperty("files");
        bool sharded = Primary.EndsWith(".safetensors.index.json", StringComparison.Ordinal);
        CheckTokenizer(registration.TokenizerSha256, artifacts);
        if (!sharded) CheckPrimary(root, path, registration.WeightsSha256, artifacts);
        VerifyArtifacts(root, artifacts, sharded);
        if (sharded) OpenShards(root, registration.WeightsSha256);
    }
    private void VerifyArtifacts(string root, JsonElement artifacts, bool sharded)
    {
        foreach (var entry in artifacts.EnumerateObject())
        {
            if (sharded && entry.Name == "model.safetensors") continue;
            string digest = entry.Value.GetProperty("sha256").GetString()!;
            Add(root, entry.Name, digest);
        }
    }
    private static void CheckTokenizer(string expected, JsonElement artifacts)
    {
        if (!StringComparer.OrdinalIgnoreCase.Equals(expected, artifacts.GetProperty("tokenizer.json").GetProperty("sha256").GetString()))
            throw new InvalidDataException("Tokenizer digest differs from the pinned snapshot.");
    }

    private void CheckPrimary(string root, string path, string hash, JsonElement artifacts)
    {
        if (!Path.GetFullPath(path).Equals(Path.GetFullPath(Path.Combine(root, Primary)), OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new InvalidDataException("Unexpected checkpoint name.");
        if (!artifacts.TryGetProperty(Primary, out var entry) || !StringComparer.OrdinalIgnoreCase.Equals(hash, entry.GetProperty("sha256").GetString()))
            throw new InvalidDataException("Unrecognized checkpoint graph or weights.");
    }

    private void OpenShards(string root, string lockHash)
    {
        string lockName = Primary + ".lock.json";
        Add(root, lockName, lockHash);
        using var manifest = Json(lockName);
        var files = manifest.RootElement;
        Add(root, Primary, files.GetProperty(Primary).GetString()!);
        using var index = Json(Primary);
        var names = Shards(index.RootElement.GetProperty("weight_map"));
        if (names.Count == 0 || names.Count > 128 || files.GetPropertyCount() != names.Count + 1)
            throw new InvalidDataException("Shard lock and index disagree.");
        foreach (var name in names) Add(root, name, files.GetProperty(name).GetString()!);
    }
    private static HashSet<string> Shards(JsonElement map)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach(var entry in map.EnumerateObject()) names.Add(SafeShardName(entry.Value.GetString()));
        return names;
    }

    private static string SafeShardName(string? value)
    {
        if (value is null || !value.EndsWith(".safetensors", StringComparison.Ordinal) || value.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('.' or '-' or '_')))
            throw new InvalidDataException("Invalid shard name.");
        return value;
    }

    private void Add(string root, string name, string digest)
    {
        var stream = File.OpenRead(Path.Combine(root, name));
        try { ProfileReader.VerifyWeights(stream, digest); streams.Add(name, stream); }
        catch { stream.Dispose(); throw; }
    }

    internal JsonDocument Json(string name)
    {
        var stream = streams[name];
        if (stream.Length > 32 * 1024 * 1024) throw new InvalidDataException("Snapshot JSON exceeds its size limit.");
        stream.Position = 0;
        return JsonDocument.Parse(stream, new JsonDocumentOptions { AllowDuplicateProperties = false });
    }

    internal static Stream Resource(string name) => typeof(SnapshotFiles).Assembly.GetManifestResourceStream("AIKernel.Dense." + name)
        ?? throw new InvalidDataException("Missing built-in snapshot data.");
    private static JsonDocument ResourceJson(string reader)
    {
        using var stream = Resource(reader + ".json");
        return JsonDocument.Parse(stream);
    }
    public void Dispose() { foreach (var stream in streams.Values) stream.Dispose(); }
}
