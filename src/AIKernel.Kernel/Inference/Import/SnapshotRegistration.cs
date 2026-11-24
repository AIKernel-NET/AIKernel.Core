namespace AIKernel.Kernel.Inference;

internal static class SnapshotRegistration
{
    internal static DenseModelSession OpenSession(DenseModelRegistration registration, string path, int threads, int context, GgufMemoryOptions? memory, long? budget)
        => ModelPlacement.OpenDense(new(path,registration),threads,context,memory,DeviceBudget(registration._document.ReaderId,budget));
    internal static void CheckDigest(string value)
    {
        if (value is not { Length: 64 } || !value.All(Uri.IsHexDigit)) throw new InvalidDataException("Expected a SHA-256 digest.");
    }
    internal static void Check(DenseRegistrationDocument document, string modelId)
    {
        if (document.ModelId != modelId || !Supported(document)) throw new InvalidDataException("Unknown registered model, reader or revision.");
    }
    private static bool Supported(DenseRegistrationDocument d) => d.ReaderId switch
    {
        "gguf" => d.Version == "Q1_0-1" && d.TokenizerVersion == "qwen2-bpe-1",
        "onnx" => Matches(d, "79346d370dfae34801d26993fda9ae2372900fd0"),
        "safetensors" => Matches(d, "c1899de289a04d12100db370d81485cdf75e47ca"),
        _ => false
    };
    private static bool Matches(DenseRegistrationDocument d, string revision) =>
        d.ModelId == "qwen3-0.6b" && d.Version == revision && d.TokenizerVersion == revision;

    internal static long DeviceBudget(string reader, long? requested)
    {
        long budget = PlacementBudget.Resolve(requested);
        return reader == "gguf" ? budget : 0;
    }
    internal static IDenseModelWeights Open(string path, int context, int? device, long budget, bool vulkan, bool q8,
        CudaQ8Kernel? kernel, GgufMemoryOptions? memory, DenseModelRegistration? registration)
    {
        var document = registration?._document;
        if (document is null || document.ReaderId == "gguf")
            return GgufDenseWeights.Open(path, context, device, budget, vulkan, q8, kernel, memory, registration);
        if (q8 || device.HasValue) throw new NotSupportedException("Snapshot registration requires CPU FP32 activations.");
        return OpenHost(path, document, context, memory);
    }
    private static SnapshotWeights OpenHost(string path,DenseRegistrationDocument profile,int context,GgufMemoryOptions? memory)
    {
        try { return SnapshotWeights.Open(path,profile,context,memory); }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or KeyNotFoundException or FormatException or OverflowException or InvalidOperationException)
        { throw new InvalidDataException("Invalid registered snapshot structure.",ex); }
    }
}
