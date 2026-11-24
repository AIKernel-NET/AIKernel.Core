using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AIKernel.Kernel.Inference;

/// <summary>Immutable host registration for a versioned dense checkpoint and its embedded tokenizer.</summary>
public sealed class DenseModelRegistration
{
    private readonly DenseRegistrationDocument _document;

    private DenseModelRegistration(DenseRegistrationDocument document) => _document = document;

    /// <summary>Parses a standalone registration. Inheritance is outside this schema.</summary>
    public static DenseModelRegistration Parse(string json, string registeredModelId)
    {
        ArgumentNullException.ThrowIfNull(json);
        var document = ReadDocument(json);
        document.Validate(registeredModelId);
        return new(document);
    }

    private static DenseRegistrationDocument ReadDocument(string json)
    {
        try
        {
            return JsonSerializer.Deserialize(json, DenseRegistrationJson.Default.DenseRegistrationDocument)
                ?? throw new InvalidDataException("A model registration is required.");
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("Invalid standalone model registration.", error);
        }
    }

    /// <summary>Opens and verifies the registered model before making it available for requests.</summary>
    public Qwen3CpuModel Open(string path, int threads, int context, GgufMemoryOptions? memory = null)
        => new(new DenseCheckpoint(path, this), threads, context, null, 0, instructionSet: "auto", memory: memory);

    internal void CheckWeights(Stream stream)
    {
        var digest = SHA256.HashData(stream);
        stream.Position = 0;
        Verify(digest, _document.WeightsSha256);
    }

    internal void CheckTokenizer(GgufWeights weights) =>
        Verify(EmbeddedTokenizerDigest.Compute(weights), _document.TokenizerSha256);

    internal Qwen3ModelDefinition BindShape(Qwen3ModelDefinition checkpoint) => _document.Shape.Bind(checkpoint);

    private static void Verify(byte[] actual, string expected)
    {
        if (!CryptographicOperations.FixedTimeEquals(actual, Convert.FromHexString(expected)))
            throw new InvalidDataException("Registered artifact digest mismatch.");
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed record DenseRegistrationDocument(string ModelId, string Version, string ReaderId, string TokenizerVersion,
    string WeightsSha256, string TokenizerSha256, DenseRegistrationShape Shape)
{
    internal void Validate(string registeredModelId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(registeredModelId);
        if (ModelId != registeredModelId || ReaderId != "gguf")
            throw new InvalidDataException("Unknown registered model or reader.");
        if (Version != "Q1_0-1" || TokenizerVersion != "qwen2-bpe-1")
            throw new InvalidDataException("Unsupported checkpoint or tokenizer revision.");
        CheckDigest(WeightsSha256);
        CheckDigest(TokenizerSha256);
        ArgumentNullException.ThrowIfNull(Shape);
    }

    private static void CheckDigest(string value)
    {
        if (value is not { Length: 64 } || !value.All(Uri.IsHexDigit))
            throw new InvalidDataException("Expected a SHA-256 digest.");
    }
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
