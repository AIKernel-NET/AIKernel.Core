using AIKernel.Kernel.Inference;
using System.Runtime.Intrinsics.X86;

namespace AIKernel.Core.Tests.Inference;

public sealed class CpuInstructionSetTests
{
    [Fact]
    public void DefaultRemainsAutomaticAndDoesNotChangePrecision()
    {
        string expected=Avx512F.IsSupported?"avx512":Avx2.IsSupported&&Fma.IsSupported?"avx2":Avx.IsSupported?"avx":"scalar";
        Assert.Equal(expected,CpuInstructionSets.Resolve());
        Assert.DoesNotContain("q8",CpuInstructionSets.Resolve());
        Assert.True(new CpuAutoTuningOptions("profile",new string('a',64)).TuneThreadCount);
    }
    [Fact]
    public void EveryAdvertisedChoiceResolvesWithoutOpeningAModel()
    {
        foreach(string choice in CpuInstructionSets.Available)
        {
            string resolved=CpuInstructionSets.Resolve(choice);
            Assert.Contains(resolved,CpuInstructionSets.Available);
            if(choice is not ("auto" or "q8-auto"))Assert.Equal(choice,resolved);
        }
    }
    [Theory]
    [InlineData("avx512")]
    [InlineData("avx-vnni-q8")]
    [InlineData("avx512-vnni-q8")]
    [InlineData("amx-int8-q8")]
    public void UnsupportedForcedChoiceFailsBeforeFileAccess(string choice)
    {
        if(CpuInstructionSets.Available.Contains(choice))return;
        Assert.Throws<PlatformNotSupportedException>(()=>new Qwen3CpuModel("does-not-exist.gguf",4,1024,choice));
    }
    [Theory]
    [InlineData("AVX2")]
    [InlineData("avx513")]
    [InlineData(" ")]
    public void TypoCannotSilentlySelectAuto(string choice)
    {
        Assert.Throws<ArgumentException>(()=>new Qwen3CpuModel("does-not-exist.gguf",4,1024,choice));
    }
}
