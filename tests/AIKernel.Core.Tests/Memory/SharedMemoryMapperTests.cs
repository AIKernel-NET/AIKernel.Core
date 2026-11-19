using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using AIKernel.Core.Memory;
using AIKernel.Kernel.Memory;

namespace AIKernel.Core.Tests.Memory;

public sealed class SharedMemoryMapperTests
{
    [Fact]
    public void MapsAnExistingChannelAndRejectsAnInvalidOrOversizedView()
    {
        const int capacity = 4096;
        var channel = $"aikernel-shared-{Guid.NewGuid():N}";
        if (OperatingSystem.IsWindows())
        {
            using var producer = MemoryMappedFile.CreateNew(channel, capacity);
            using var writer = producer.CreateViewAccessor();
            writer.Write(32, 0x784512ab);
            Verify(new Win32MemoryMapper());
        }
        else if (OperatingSystem.IsLinux())
        {
            var path = Path.Combine("/dev/shm", channel);
            try
            {
                using var producer = File.Create(path);
                producer.SetLength(capacity);
                producer.Position = 32;
                producer.Write(BitConverter.GetBytes(0x784512ab));
                producer.Flush();
                Verify(new PosixMemoryMapper());
            }
            finally
            {
                File.Delete(path);
            }
        }

        void Verify(ISharedMemoryMapper mapper)
        {
            Assert.Equal("shared_memory_unavailable", mapper.OpenShared("../invalid", capacity).Error?.Code);
            Assert.Equal("shared_memory_unavailable", mapper.OpenShared(channel, capacity * 2).Error?.Code);
            var opened = mapper.OpenShared(channel, capacity);
            Assert.True(opened.IsSuccess, opened.Error?.ToString());
            using var region = opened.Value!;
            Assert.Equal(capacity, region.Length);
            Assert.Equal(0x784512ab, Marshal.ReadInt32(region.Pointer, 32));
        }
    }
}
