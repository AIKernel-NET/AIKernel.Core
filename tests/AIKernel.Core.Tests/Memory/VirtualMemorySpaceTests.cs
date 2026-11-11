using System.Buffers;
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

    [Fact]
    public void BoundPagesFaultInLazilyAndDirtySlicesSpillToSwap()
    {
        using var space = new VirtualMemorySpace(3, BlockSize, 1, directory.FullName, source: new TestSource());
        Assert.Equal(0, space.GetStatistics().BackingReads);
        space.UseComputeBlock(1, (bytes, device) =>
        {
            Assert.Null(device);
            Assert.Equal(Bytes(2), bytes.ToArray());
        });
        Assert.Equal(1, space.GetStatistics().BackingReads);
        space.WriteBlockSlice(1, 3, [99]);
        AssertBlock(space, 0, 1);
        var bytes = Bytes(0);
        space.ReadBlock(1, bytes);
        Assert.Equal(99, bytes[3]);
        Assert.Equal(2, bytes[4]);
        Assert.Equal(1, space.GetStatistics().SwapReads);
        space.DiscardBlock(1);
        AssertBlock(space, 1, 2);
    }

    [Fact]
    public void ForkSharesPhysicalStorageUntilTheFirstWrite()
    {
        var device = new TestDevice();
        using var parent = new VirtualMemorySpace(3, BlockSize, 1, directory.FullName, 1, device);
        parent.WriteBlock(0, Bytes(17));
        parent.MoveBlock(0, VirtualMemoryTier.Vram);
        parent.WriteBlock(1, Bytes(32));
        parent.MoveBlock(1, VirtualMemoryTier.Swap);
        var free = parent.GetAvailableBlocks();

        using var child = Assert.IsType<VirtualMemorySpace>(parent.ForkSpace().Value);
        Assert.Equal(2, parent.GetBlockReferenceCount(0));
        Assert.Equal(2, parent.GetBlockReferenceCount(1));
        Assert.Equal(free, child.GetAvailableBlocks());
        Assert.Equal(1, device.LiveBlocks);

        child.WriteBlockSlice(0, 0, [99]);
        child.WriteBlock(1, Bytes(54));
        Assert.Equal(1, parent.GetBlockReferenceCount(0));
        AssertBlock(parent, 0, 17);
        AssertBlock(parent, 1, 32);
        parent.Dispose();

        var changed = Bytes(0);
        child.ReadBlock(0, changed);
        Assert.Equal(99, changed[0]);
        Assert.Equal(17, changed[1]);
        AssertBlock(child, 1, 54);
    }

    [Fact]
    public void DeviceHandoffPreservesRamVramAndEncryptedSwapPagesAcrossForks()
    {
        var source = new TestDevice { VerifyWipe = true };
        var target = new TestDevice { VerifyWipe = true };
        using var space = new VirtualMemorySpace(3, BlockSize, 1, directory.FullName, 1, source,
            swapPolicy: new(Encrypt: true, ZeroWipe: true));
        space.WriteBlock(0, Bytes(10));
        space.WriteBlock(1, Bytes(20));
        space.WriteBlock(2, Bytes(30));
        using var fork = space.ForkSpace().Value!;

        Assert.False(space.BindPreparedDevice(target).IsSuccess);
        Assert.Equal(1, space.PrepareDeviceChange().Value);
        Assert.Equal(0, source.LiveBlocks);
        Assert.True(space.BindPreparedDevice(target).IsSuccess);
        Assert.Equal(2, fork.GetBlockReferenceCount(0));
        AssertBlock(fork, 0, 10);
        AssertBlock(fork, 1, 20);
        AssertBlock(fork, 2, 30);
        space.MoveBlock(2, VirtualMemoryTier.Vram);
        Assert.Equal(1, target.LiveBlocks);
        Assert.Equal(0, source.LiveBlocks);
    }

    [Fact]
    public void SnapshotRestoresContinuationAndPagesWithoutInitialResidency()
    {
        var path = Path.Combine(directory.FullName, "kv.snapshot");
        using var space = new VirtualMemorySpace(4, BlockSize, 1, directory.FullName);
        space.WriteBlock(2, Bytes(12));
        space.WriteBlock(3, Bytes(15));
        var manifest = space.Hibernate(path, [0x10, 0x20]);
        Assert.True(manifest.IsSuccess, manifest.Error?.ToString());
        Assert.Equal(2, manifest.Value.StoredBlocks);

        var restore = VirtualMemorySpace.Restore(path, ramBlocks: 1);
        Assert.True(restore.IsSuccess, restore.Error?.ToString());
        using (var restored = restore.Value!)
        {
            Assert.Equal((1, 0), restored.GetAvailableBlocks());
            Span<byte> continuation = stackalloc byte[2];
            Assert.Equal(2, restored.ReadSnapshotState(continuation).Value);
            Assert.Equal(new byte[] { 0x10, 0x20 }, continuation.ToArray());
            restored.UseComputeBlock(2, (data, device) =>
            {
                Assert.Null(device);
                Assert.Equal(Bytes(12), data.ToArray());
            });
            Assert.Equal((1, 0), restored.GetAvailableBlocks());
            AssertBlock(restored, 0, 0);
            AssertBlock(restored, 3, 15);
        }

        var corrupt = File.ReadAllBytes(path);
        corrupt[^33] ^= 0x80;
        File.WriteAllBytes(path, corrupt);
        Assert.Equal("snapshot_integrity_failed", VirtualMemorySpace.Restore(path).Error?.Code);
        File.WriteAllBytes(path, corrupt[..31]);
        Assert.Equal("snapshot_invalid", VirtualMemorySpace.Restore(path).Error?.Code);
    }

    [Fact]
    public void SecureSpaceClearsHostAndDeviceBlocksBeforeRelease()
    {
        var device = new TestDevice { VerifyWipe = true };
        using (var space = new VirtualMemorySpace(3, BlockSize, 1, directory.FullName, 1, device,
                   swapPolicy: new(Encrypt: true, ZeroWipe: true)))
        {
            space.WriteBlock(0, Bytes(0xBE));
            space.MoveBlock(0, VirtualMemoryTier.Vram);
            space.WriteBlock(1, Bytes(0xEF));
            space.MoveBlock(1, VirtualMemoryTier.Swap);
            AssertBlock(space, 1, 0xEF);
            Assert.Equal("snapshot_plaintext_disabled", space.Hibernate(Path.Combine(directory.FullName, "kv.snapshot")).Error?.Code);
        }
        Assert.True(device.WipedDeviceBlocks > 0);
        Assert.True(device.WipedHostBlocks > 0);
        Assert.Equal(0, device.LiveBlocks);
        Assert.Empty(directory.EnumerateFileSystemInfos());
    }

    public void Dispose() => directory.Delete(recursive: true);

    private sealed class TestSource : IVirtualMemorySource
    {
        public void ReadBlock(long blockIndex, Span<byte> destination) => destination.Fill((byte)(blockIndex + 1));
    }

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

    private sealed class TestDevice : IZeroableVirtualMemoryDevice, IVirtualMemoryHostAllocator
    {
        internal bool RejectUploads { get; init; }
        internal bool VerifyWipe { get; init; }
        internal int WipedDeviceBlocks { get; private set; }
        internal int WipedHostBlocks { get; private set; }

        public void ZeroScratch() { }

        public IMemoryOwner<byte> AllocateHostBlock(int length) => new HostTestBlock(this, length);
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

        private sealed class TestBlock(TestDevice owner, byte[] bytes) : IZeroableDeviceBlock
        {
            private byte[]? content = bytes;

            public void Download(Span<byte> destination)
                => (content ?? throw new ObjectDisposedException(nameof(TestBlock))).CopyTo(destination);

            public void Zero() => content.AsSpan().Clear();

            public void Dispose()
            {
                if (content is not null)
                {
                    if (owner.VerifyWipe)
                    {
                        Assert.All(content, b => Assert.Equal(0, b));
                        owner.WipedDeviceBlocks++;
                    }
                    content = null;
                    owner.LiveBlocks--;
                }
            }
        }

        private sealed class HostTestBlock(TestDevice owner, int length) : IMemoryOwner<byte>
        {
            public Memory<byte> Memory { get; } = new byte[length];

            public void Dispose()
            {
                if (!owner.VerifyWipe) return;
                Assert.True(Memory.Span.IndexOfAnyExcept((byte)0) < 0);
                owner.WipedHostBlocks++;
            }
        }
    }
}
