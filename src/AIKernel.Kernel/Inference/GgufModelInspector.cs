using System.Collections.Immutable;

namespace AIKernel.Kernel.Inference;

/// <summary>One stored tensor; decoder availability does not imply model generation support.</summary>
public sealed record GgufTensorInfo(string Name, ImmutableArray<long> Dimensions, int Type,
    string Encoding, long Offset, long StoredBytes, bool HasCpuReferenceDecoder);

/// <summary>Validated file inventory without loading weights, tokenizer, KV state, or a GPU runtime.</summary>
public sealed record GgufModelInfo(string Architecture, string? TokenizerModel, string? TokenizerPre,
    long FileBytes, long StoredTensorBytes, long StoredParameters, ImmutableArray<GgufTensorInfo> Tensors);

/// <summary>Inspects GGUF storage independently of the supported execution architectures.</summary>
public static class GgufModelInspector
{
    /// <summary>Reads only the bounded GGUF header and validates tensor ranges against file length.
    /// This is not a model-load admission check or a resident-memory estimate.</summary>
    public static GgufModelInfo Inspect(string path, GgufReadLimits? limits = null)
    {
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        return Inspect(stream,limits);
    }
    /// <summary>Inspects a seekable stream whose Length is the full file size. Leaves the stream open.
    /// Only directory bytes are read; tensor payloads are not validated for numerical correctness.</summary>
    public static GgufModelInfo Inspect(Stream stream, GgufReadLimits? limits = null)
    {
        ArgumentNullException.ThrowIfNull(stream);
        var directory=GgufDirectory.Read(stream,limits);
        var tensors=directory.Tensors.OrderBy(x=>x.Key,StringComparer.Ordinal).Select(x=>new GgufTensorInfo(
            x.Key,x.Value.Dimensions,x.Value.Type,x.Value.Encoding.Name,x.Value.Offset,x.Value.ByteLength,x.Value.Encoding.CpuReference)).ToImmutableArray();
        return new(directory.Metadata.GetValueOrDefault("general.architecture") as string??"unknown",
            directory.Metadata.GetValueOrDefault("tokenizer.ggml.model") as string,
            directory.Metadata.GetValueOrDefault("tokenizer.ggml.pre") as string,
            directory.Length,tensors.Sum(x=>x.StoredBytes),directory.Parameters,tensors);
    }
}
