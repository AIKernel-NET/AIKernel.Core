namespace AIKernel.Cuda.Libtorch.Cuda13.Model;

public sealed record LlamaModelConfig(
    string ModelPath,
    int MaxInputTokens = 4096,
    int MaxOutputTokens = 64,
    string Device = "cuda");
