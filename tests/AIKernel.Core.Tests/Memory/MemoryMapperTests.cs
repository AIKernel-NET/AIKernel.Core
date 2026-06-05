namespace AIKernel.Core.Tests.Memory;

using AIKernel.Common.Results;
using AIKernel.Core.Memory;
using AIKernel.Kernel;
using AIKernel.Kernel.Memory;
using Microsoft.Extensions.DependencyInjection;

public sealed class MemoryMapperTests
{
    [Fact]
    public void MemoryMapperBase_RejectsBlankPathWithoutOpeningCore()
    {
        var mapper = new CountingMemoryMapper();

        var result = mapper.Open(" ");

        Assert.True(result.IsFailure);
        Assert.Equal("MEMORY_MAPPING_ERROR", result.Error!.Code);
        Assert.Equal(FailureKind.FailClosed, result.Error.FailureKind);
        Assert.Equal(0, mapper.OpenCoreCallCount);
    }

    [Fact]
    public void AddAIKernelKernel_RegistersOperatingSystemMemoryMapper()
    {
        var services = new ServiceCollection();

        services.AddAIKernelKernel();

        using var provider = services.BuildServiceProvider();
        var mapper = provider.GetRequiredService<IMemoryMapper>();

        if (OperatingSystem.IsWindows())
        {
            Assert.IsType<Win32MemoryMapper>(mapper);
        }
        else
        {
            Assert.IsType<PosixMemoryMapper>(mapper);
        }
    }

    [Fact]
    public void Win32MemoryMapper_MapsAndUnmapsFileOnWindows()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var path = Path.Combine(
            Path.GetTempPath(),
            $"aikernel-memory-map-{Guid.NewGuid():N}.bin");
        File.WriteAllBytes(path, [1, 2, 3, 4]);

        try
        {
            var mapper = new Win32MemoryMapper();

            var result = mapper.Open(path);

            Assert.True(result.IsSuccess);
            using var region = result.Value!;
            Assert.True(region.IsMapped);
            Assert.NotEqual(IntPtr.Zero, region.Pointer);
            Assert.Equal(4, region.Length);
            Assert.Equal(MemoryAccessMode.Read, region.Info.AccessMode);

            var unmap = region.Unmap();
            Assert.True(unmap.IsSuccess);
            Assert.False(region.IsMapped);

            var secondUnmap = region.Unmap();
            Assert.True(secondUnmap.IsSuccess);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class CountingMemoryMapper : MemoryMapperBase
    {
        public int OpenCoreCallCount { get; private set; }

        protected override Result<IMemoryRegion> OpenCore(
            string path,
            MemoryAccessMode accessMode)
        {
            OpenCoreCallCount++;
            return Result<IMemoryRegion>.Fail(new ErrorContext(
                "unexpected",
                "UNEXPECTED",
                false));
        }
    }
}
