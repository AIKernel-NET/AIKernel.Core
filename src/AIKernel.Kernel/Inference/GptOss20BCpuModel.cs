namespace AIKernel.Kernel.Inference;

/// <summary>Compatibility entry point for direct model construction through the 0.3 series.</summary>
[Obsolete("Use ModelRegistration.GptOss20B.OpenSession(...). This type will be removed in 0.4.", false)]
public sealed class GptOss20BCpuModel : MixtureModelSession
{
    public GptOss20BCpuModel(string modelPath, int cpuThreads = 4, int contextTokens = 512)
        : base(modelPath, cpuThreads, contextTokens) { }

    public GptOss20BCpuModel(string modelPath, int cpuThreads, int contextTokens, GgufMemoryOptions? memory)
        : base(modelPath, cpuThreads, contextTokens, memory) { }
}
