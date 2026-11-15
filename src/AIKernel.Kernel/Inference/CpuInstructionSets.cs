using System.Runtime.Intrinsics.X86;

namespace AIKernel.Kernel.Inference;

/// <summary>Explicit Q1_0 matrix instruction choices, checked against both CPU and OS support.
/// Q8 choices also change activation precision; auto preserves FP32 activations.</summary>
public static class CpuInstructionSets
{
    internal static readonly string[] Known=["auto","scalar","avx","avx2","avx512","q8-auto",
        "scalar-q8","avx-q8","avx2-q8","avx512-q8","avx2-int8-q8","avx-vnni-q8","avx512-vnni-q8","amx-int8-q8"];
    /// <summary>Choices usable by this process. AMX remains opt-in pending real-device validation.</summary>
    public static IReadOnlyList<string> Available
    {
        get
        {
            var result=new List<string>{"auto","scalar"};
            if(Avx.IsSupported)result.Add("avx");
            if(Avx2.IsSupported&&Fma.IsSupported)result.Add("avx2");
            if(Avx512F.IsSupported)result.Add("avx512");
            result.Add("q8-auto");result.AddRange(Int8CpuMatrix.Candidates());return result.AsReadOnly();
        }
    }
    /// <summary>Resolves the initial supported backend without opening a model. Auto can subsequently be calibrated with TuneCpuAsync.</summary>
    public static string Resolve(string instructionSet="auto")
    {
        ArgumentException.ThrowIfNullOrEmpty(instructionSet);
        return QuantizedCpuMatrix.Resolve(instructionSet);
    }
}
