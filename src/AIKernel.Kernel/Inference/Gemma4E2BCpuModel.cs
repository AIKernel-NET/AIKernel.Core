namespace AIKernel.Kernel.Inference;

/// <summary>Compatibility entry point for direct model construction through the 0.3 series.</summary>
[Obsolete("Use ModelRegistration.Gemma4E2B.OpenSession(...). This type will be removed in 0.4.", false)]
public sealed class Gemma4E2BCpuModel : GemmaTextSession
{
    public Gemma4E2BCpuModel(string modelPath,int cpuThreads=4,int contextTokens=1024,long maximumWeightBytes=4L*1024*1024*1024)
        : base(modelPath, cpuThreads, contextTokens, maximumWeightBytes) { }

    public Gemma4E2BCpuModel(string modelPath,int cpuThreads,int contextTokens,long maximumWeightBytes,Gemma4ExecutionOptions execution)
        : base(modelPath, cpuThreads, contextTokens, maximumWeightBytes, execution) { }
}
