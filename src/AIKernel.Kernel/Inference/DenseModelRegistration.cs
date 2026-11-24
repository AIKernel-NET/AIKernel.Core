using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIKernel.Kernel.Inference;

/// <summary>Immutable host registration for a versioned dense checkpoint and its embedded tokenizer.</summary>
public sealed class DenseModelRegistration : IModelRegistration<DenseModelSession>
{
    internal readonly DenseRegistrationDocument _document;

    private DenseModelRegistration(DenseRegistrationDocument document) => _document = document;

    /// <summary>Parses a complete registration or resolves a named profile within a host-supplied catalog.</summary>
    public static DenseModelRegistration Parse(string json, string registeredModelId)
        => new(ProfileReader.ReadVerified(json, registeredModelId));

    /// <summary>Opens and verifies the registered model before making it available for requests.</summary>
    [Obsolete("Use OpenSession instead. Open will be removed in 0.4.", false)]
    public Qwen3CpuModel Open(string path, int threads, int context, GgufMemoryOptions? memory = null)
        => new(this, path, threads, context, memory);

    /// <inheritdoc />
    public DenseModelSession OpenSession(string path, int threads = 4, int context = 1024,
        GgufMemoryOptions? memory = null, long? deviceBudgetBytes = null)
        => SnapshotRegistration.OpenSession(this, path, threads, context, memory, deviceBudgetBytes);

    /// <summary>Uses the Kernel's configured device allocation budget.</summary>
    public DenseModelSession OpenSession(string path, int threads, int context, GgufMemoryOptions? memory)
        => OpenSession(path, threads, context, memory, null);

    internal void CheckWeights(Stream stream)
        => ProfileReader.VerifyWeights(stream, _document.WeightsSha256);

    internal void CheckTokenizer(GgufWeights weights) =>
        ProfileReader.VerifyTokenizer(weights, _document.TokenizerSha256);

    internal Qwen3ModelDefinition BindShape(Qwen3ModelDefinition checkpoint) => _document.Shape.Bind(checkpoint);


}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record DenseRegistrationDocument(string ModelId, string Version, string ReaderId, string TokenizerVersion,
    string WeightsSha256, string TokenizerSha256, DenseRegistrationShape Shape)
{
    internal void Validate(string registeredModelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registeredModelId);
        SnapshotRegistration.Check(this, registeredModelId);
        CheckDigest(WeightsSha256);
        CheckDigest(TokenizerSha256);
        ArgumentNullException.ThrowIfNull(Shape);
    }


    private static void CheckDigest(string value)
        => SnapshotRegistration.CheckDigest(value);
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record DenseRegistrationShape(int Width, int Hidden, int Heads, int KvHeads, int HeadSize, int Layers, float Epsilon)
{
    internal Qwen3ModelDefinition Bind(Qwen3ModelDefinition model)
    {
        var declared = new Qwen3ModelDefinition(Width, Hidden, Heads, KvHeads, HeadSize, Layers, Epsilon);
        if (declared != model) throw new InvalidDataException("Registered graph and checkpoint dimensions differ.");
        return declared;
    }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, AllowDuplicateProperties = false,
    RespectRequiredConstructorParameters = true, RespectNullableAnnotations = true)]
[JsonSerializable(typeof(DenseRegistrationDocument))]
internal sealed partial class DenseRegistrationJson : JsonSerializerContext;

internal static class EmbeddedTokenizerDigest
{
    // Length-prefixed UTF-8 and little-endian Int32; order is part of the registration schema.
    internal static byte[] Compute(GgufWeights weights)
    {
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendText(digest, (string)weights.Metadata["tokenizer.ggml.model"]);
        AppendText(digest, (string)weights.Metadata["tokenizer.ggml.pre"]);
        AppendInt(digest, weights.Int("tokenizer.ggml.eos_token_id"));
        AppendTable(digest, weights.Tokens);
        AppendTable(digest, weights.Merges);
        return digest.GetHashAndReset();
    }

    private static void AppendTable(IncrementalHash digest, string[] table)
    {
        AppendInt(digest, table.Length);
        foreach (var entry in table) AppendText(digest, entry);
    }

    private static void AppendText(IncrementalHash digest, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        AppendInt(digest, bytes.Length);
        digest.AppendData(bytes);
    }

    private static void AppendInt(IncrementalHash digest, int value)
    {
        Span<byte> bytes = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        digest.AppendData(bytes);
    }
}
