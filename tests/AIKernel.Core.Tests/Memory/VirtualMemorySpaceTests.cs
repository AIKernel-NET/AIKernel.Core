using AIKernel.Core.Memory;
using AIKernel.Kernel.Memory;

namespace AIKernel.Core.Tests.Memory;

public sealed class VirtualMemorySpaceTests : IDisposable
{
    private const int BlockSize = 16;
    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("aikernel-vmem-");

    [Fact]
    public void RamPressureUsesLruAndSparseSwapAddresses()
    {
        const long last = 1_000_000_000;
        using (var space = new VirtualMemorySpace(last + 1, BlockSize, 2, directory.FullName))
        {
            AssertBlock(space, last, 0);
            Assert.Equal(VirtualMemoryTier.Unallocated, space.GetBlockTier(last));
            space.WriteBlock(0, Bytes(1));
            space.WriteBlock(1, Bytes(2));
            AssertBlock(space, 0, 1);

            space.WriteBlock(last, Bytes(3));
            Assert.Equal(VirtualMemoryTier.Swap, space.GetBlockTier(1));
            Assert.Equal(VirtualMemoryTier.Ram, space.GetBlockTier(0));
            AssertBlock(space, 1, 2);
            Assert.Equal(VirtualMemoryTier.Swap, space.GetBlockTier(0));
            AssertBlock(space, last, 3);

            space.WriteBlock(0, Bytes(4));
            space.MoveBlock(0, VirtualMemoryTier.Swap);
            AssertBlock(space, 0, 4);
            AssertBlock(space, 1, 2);
            AssertBlock(space, last, 3);
        }

        Assert.Empty(directory.EnumerateFileSystemInfos());
    }

    [Fact]
    public void PagesRoundTripThroughVramAndSwap()
    {
        var device = new TestDevice();
        using (var space = new VirtualMemorySpace(3, BlockSize, 1, directory.FullName, 1, device))
        {
            space.WriteBlock(0, Bytes(10));
            space.WriteBlock(1, Bytes(20));
            Assert.Equal(VirtualMemoryTier.Vram, space.GetBlockTier(0));
            space.WriteBlock(2, Bytes(30));
            Assert.Equal(VirtualMemoryTier.Swap, space.GetBlockTier(0));
            Assert.Equal(VirtualMemoryTier.Vram, space.GetBlockTier(1));
            Assert.Equal(1, device.LiveBlocks);

            AssertBlock(space, 1, 20);
            Assert.Equal(VirtualMemoryTier.Ram, space.GetBlockTier(1));
            Assert.Equal(VirtualMemoryTier.Swap, space.GetBlockTier(2));

            space.MoveBlock(0, VirtualMemoryTier.Vram);
            Assert.Equal(VirtualMemoryTier.Vram, space.GetBlockTier(0));
            space.MoveBlock(0, VirtualMemoryTier.Swap);
            AssertBlock(space, 0, 10);
            AssertBlock(space, 2, 30);
            AssertBlock(space, 1, 20);
        }

        Assert.Equal(0, device.LiveBlocks);
        Assert.Empty(directory.EnumerateFileSystemInfos());
    }

    [Fact]
    public void VramExhaustionFallsBackToSwapDuringRamEviction()
    {
        var device = new TestDevice { RejectUploads = true };
        using var space = new VirtualMemorySpace(2, BlockSize, 1, directory.FullName, 1, device);
        space.WriteBlock(0, Bytes(10));
        Assert.Throws<OutOfMemoryException>(() => space.MoveBlock(0, VirtualMemoryTier.Vram));
        Assert.Equal(VirtualMemoryTier.Ram, space.GetBlockTier(0));

        space.WriteBlock(1, Bytes(20));
        Assert.Equal(VirtualMemoryTier.Swap, space.GetBlockTier(0));
        AssertBlock(space, 0, 10);
        AssertBlock(space, 1, 20);
        Assert.Equal(0, device.LiveBlocks);
    }

    [Fact]
    public void SwapFailurePreservesTheVictimAndAllowsRetry()
    {
        var swap = Path.Combine(directory.FullName, "swap");
        using (var space = new VirtualMemorySpace(2, BlockSize, 1, swap))
        {
            space.WriteBlock(0, Bytes(10));
            Assert.Throws<DirectoryNotFoundException>(() => space.WriteBlock(1, Bytes(20)));
            Assert.Equal(VirtualMemoryTier.Ram, space.GetBlockTier(0));
            Assert.Equal(VirtualMemoryTier.Unallocated, space.GetBlockTier(1));
            AssertBlock(space, 0, 10);

            Directory.CreateDirectory(swap);
            space.WriteBlock(1, Bytes(20));
            AssertBlock(space, 0, 10);
            AssertBlock(space, 1, 20);
        }

        Directory.Delete(swap);
    }

    [Fact]
    public void ConcurrentBlockAccessPreservesEachBlock()
    {
        var device = new TestDevice();
        using var space = new VirtualMemorySpace(32, BlockSize, 2, directory.FullName, 2, device);
        Parallel.For(0, 32, i => space.WriteBlock(i, Bytes((byte)i)));
        Parallel.For(0, 32, i => AssertBlock(space, i, (byte)i));
        Assert.InRange(device.LiveBlocks, 0, 2);
    }

    [Fact]
    public void InvalidRangesAndDisposedSpacesRejectAccess()
    {
        using var space = new VirtualMemorySpace(2, BlockSize, 1, directory.FullName);
        Assert.Throws<ArgumentOutOfRangeException>(() => space.ReadBlock(-1, Bytes(0)));
        Assert.Throws<ArgumentOutOfRangeException>(() => space.WriteBlock(2, Bytes(0)));
        Assert.Throws<ArgumentException>(() => space.WriteBlock(0, [1]));
        Assert.Throws<InvalidOperationException>(() => space.MoveBlock(0, VirtualMemoryTier.Vram));
        space.Dispose();
        Assert.Throws<ObjectDisposedException>(() => space.ReadBlock(0, Bytes(0)));
    }

    public void Dispose() => directory.Delete(recursive: true);

    private static byte[] Bytes(byte value)
    {
        var result = new byte[BlockSize];
        result.AsSpan().Fill(value);
        return result;
    }

    private static void AssertBlock(VirtualMemorySpace space, long index, byte value)
    {
        var bytes = Bytes(255);
        space.ReadBlock(index, bytes);
        Assert.Equal(Bytes(value), bytes);
    }

    private sealed class TestDevice : IVirtualMemoryDevice
    {
        internal bool RejectUploads { get; init; }
        internal int LiveBlocks { get; private set; }

        public IVirtualMemoryDeviceBlock Upload(ReadOnlySpan<byte> source)
        {
            if (RejectUploads)
            {
                throw new OutOfMemoryException();
            }

            LiveBlocks++;
            return new TestBlock(this, source.ToArray());
        }

        private sealed class TestBlock(TestDevice owner, byte[] bytes) : IVirtualMemoryDeviceBlock
        {
            private byte[]? content = bytes;

            public void Download(Span<byte> destination)
                => (content ?? throw new ObjectDisposedException(nameof(TestBlock))).CopyTo(destination);

            public void Dispose()
            {
                if (content is not null)
                {
                    content = null;
                    owner.LiveBlocks--;
                }
            }
        }
    }
}
