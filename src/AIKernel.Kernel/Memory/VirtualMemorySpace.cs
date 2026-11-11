using AIKernel.Core.Memory;

namespace AIKernel.Kernel.Memory;

/// <summary>A logical address space backed by RAM, optional VRAM, and a private swap file.</summary>
public sealed class VirtualMemorySpace : IDisposable
{
    private readonly Lock gate = new();
    private readonly Dictionary<long, Block> blocks = [];
    private readonly LinkedList<Block> ram = new();
    private readonly LinkedList<Block> vram = new();
    private readonly Stack<long> freeSlots = new();
    private readonly string swapDirectory;
    private readonly int ramCapacity;
    private readonly int vramCapacity;
    private readonly IVirtualMemoryDevice? device;
    private FileStream? swapFile;
    private long nextSlot;
    private long swapReads;
    private long devicePageIns;
    private bool disposed;

    /// <summary>The number of addressable blocks.</summary>
    public long BlockCount { get; }

    /// <summary>The number of bytes in each block.</summary>
    public int BlockSize { get; }

    /// <summary>
    /// Creates a block address space. The caller keeps the optional device alive until the space is disposed.
    /// </summary>
    public VirtualMemorySpace(long blockCount, int blockSize, int ramBlocks, string swapDirectory,
        int vramBlocks = 0, IVirtualMemoryDevice? device = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(blockCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(blockSize);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(blockCount, long.MaxValue / blockSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ramBlocks);
        ArgumentOutOfRangeException.ThrowIfNegative(vramBlocks);
        ArgumentException.ThrowIfNullOrWhiteSpace(swapDirectory);
        if (vramBlocks > 0)
        {
            ArgumentNullException.ThrowIfNull(device);
        }

        BlockCount = blockCount;
        BlockSize = blockSize;
        ramCapacity = ramBlocks;
        vramCapacity = vramBlocks;
        this.swapDirectory = Path.GetFullPath(swapDirectory);
        this.device = device;
    }

    /// <summary>Reads a whole block and promotes a resident copy to RAM on a page fault.</summary>
    public void ReadBlock(long blockIndex, Span<byte> destination)
    {
        ValidateIndex(blockIndex);
        ValidateSize(destination.Length, nameof(destination));
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!blocks.TryGetValue(blockIndex, out var block))
            {
                destination.Clear();
                return;
            }

            MoveToRam(block);
            block.Ram!.AsSpan().CopyTo(destination);
        }
    }

    /// <summary>Replaces a whole block in RAM.</summary>
    public void WriteBlock(long blockIndex, ReadOnlySpan<byte> source)
    {
        ValidateIndex(blockIndex);
        ValidateSize(source.Length, nameof(source));
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (blocks.TryGetValue(blockIndex, out var block))
            {
                if (block.Ram is { } bytes)
                {
                    source.CopyTo(bytes);
                    TouchRam(block);
                    return;
                }

                InstallRam(block, source.ToArray());
                return;
            }

            var created = new Block();
            blocks.EnsureCapacity(checked(blocks.Count + 1));
            InstallRam(created, source.ToArray());
            blocks.Add(blockIndex, created);
        }
    }

    /// <summary>Returns a block's current tier without changing its eviction order.</summary>
    public VirtualMemoryTier GetBlockTier(long blockIndex)
    {
        ValidateIndex(blockIndex);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return blocks.TryGetValue(blockIndex, out var block) ? block.Tier : VirtualMemoryTier.Unallocated;
        }
    }

    /// <summary>Moves a written block to the requested tier. Unwritten blocks remain implicitly zero.</summary>
    public void MoveBlock(long blockIndex, VirtualMemoryTier destination)
    {
        ValidateIndex(blockIndex);
        if (destination is not (VirtualMemoryTier.Ram or VirtualMemoryTier.Vram or VirtualMemoryTier.Swap))
        {
            throw new ArgumentOutOfRangeException(nameof(destination));
        }

        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (destination == VirtualMemoryTier.Vram && vramCapacity == 0)
            {
                throw new InvalidOperationException("This address space has no VRAM tier.");
            }

            if (!blocks.TryGetValue(blockIndex, out var block) || block.Tier == destination)
            {
                return;
            }

            switch (destination)
            {
                case VirtualMemoryTier.Ram:
                    MoveToRam(block);
                    break;
                case VirtualMemoryTier.Vram:
                    MoveToVram(block, allowSwapFallback: false);
                    break;
                case VirtualMemoryTier.Swap:
                    MoveToSwap(block);
                    break;
            }
        }
    }

    /// <summary>Executes a synchronous operation on a written block resident in device memory.</summary>
    /// <remarks>The operation must neither retain or dispose the block nor reenter the address space.</remarks>
    public void UseDeviceBlock(long blockIndex, Action<IVirtualMemoryDeviceBlock> operation)
    {
        ValidateIndex(blockIndex);
        ArgumentNullException.ThrowIfNull(operation);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (vramCapacity == 0 || !blocks.TryGetValue(blockIndex, out var block))
            {
                throw new InvalidOperationException("A written block and a VRAM tier are required.");
            }

            if (block.Device is null)
            {
                MoveToVram(block, allowSwapFallback: false);
            }
            else
            {
                vram.Remove(block.Node);
                vram.AddLast(block.Node);
            }

            operation(block.Device!);
        }
    }

    /// <summary>Returns current residency and cumulative transfer counts.</summary>
    public VirtualMemoryStatistics GetStatistics()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return new VirtualMemoryStatistics(ram.Count, vram.Count, blocks.Count - ram.Count - vram.Count,
                swapReads, devicePageIns);
        }
    }

    /// <summary>Releases all resident blocks and deletes the swap file.</summary>
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            List<Exception>? errors = null;
            foreach (var block in vram)
            {
                try
                {
                    block.Device!.Dispose();
                }
                catch (Exception ex)
                {
                    (errors ??= []).Add(ex);
                }
            }

            blocks.Clear();
            ram.Clear();
            vram.Clear();
            freeSlots.Clear();
            swapFile?.Dispose();
            if (errors is not null)
            {
                throw new AggregateException("One or more device blocks could not be released.", errors);
            }
        }
    }

    private void MoveToRam(Block block)
    {
        if (block.Ram is not null)
        {
            TouchRam(block);
            return;
        }

        InstallRam(block, LoadBytes(block));
    }

    private void InstallRam(Block block, byte[] bytes)
    {
        if (ram.Count == ramCapacity)
        {
            var victim = ram.First!.Value;
            if (vramCapacity == 0 || !MoveToVram(victim, allowSwapFallback: true, incoming: block))
            {
                MoveToSwap(victim);
            }
        }

        ReleaseResidency(block);
        block.Ram = bytes;
        ram.AddLast(block.Node);
    }

    private bool MoveToVram(Block block, bool allowSwapFallback, Block? incoming = null)
    {
        if (vram.Count == vramCapacity)
        {
            var victim = vram.First!;
            if (ReferenceEquals(victim.Value, incoming))
            {
                // The incoming block will release its own device allocation after RAM has room.
                if (victim.Next is not { } next)
                {
                    return false;
                }

                victim = next;
            }

            MoveToSwap(victim.Value);
        }

        var bytes = block.Ram ?? LoadBytes(block);
        IVirtualMemoryDeviceBlock uploaded;
        try
        {
            uploaded = device!.Upload(bytes);
        }
        catch (OutOfMemoryException) when (allowSwapFallback)
        {
            return false;
        }

        try
        {
            ReleaseResidency(block);
        }
        catch
        {
            uploaded.Dispose();
            throw;
        }

        block.Device = uploaded;
        vram.AddLast(block.Node);
        devicePageIns++;
        return true;
    }

    private void MoveToSwap(Block block)
    {
        var bytes = block.Ram ?? LoadBytes(block);
        var file = swapFile ??= OpenSwapFile();
        var slot = freeSlots.TryPop(out var available) ? available : nextSlot++;
        try
        {
            RandomAccess.Write(file.SafeFileHandle, bytes, slot * BlockSize);
            ReleaseResidency(block);
        }
        catch
        {
            freeSlots.Push(slot);
            throw;
        }

        block.SwapSlot = slot;
    }

    private byte[] LoadBytes(Block block)
    {
        var bytes = new byte[BlockSize];
        if (block.Device is { } deviceBlock)
        {
            deviceBlock.Download(bytes);
            return bytes;
        }

        var offset = block.SwapSlot * BlockSize;
        var remaining = bytes.AsSpan();
        while (!remaining.IsEmpty)
        {
            var read = RandomAccess.Read(swapFile!.SafeFileHandle, remaining, offset);
            if (read == 0)
            {
                throw new EndOfStreamException("The swap file ended inside a block.");
            }

            remaining = remaining[read..];
            offset += read;
        }

        swapReads++;
        return bytes;
    }

    private void ReleaseResidency(Block block)
    {
        block.Device?.Dispose();
        if (block.SwapSlot >= 0)
        {
            freeSlots.Push(block.SwapSlot);
        }

        block.Node.List?.Remove(block.Node);
        block.Ram = null;
        block.Device = null;
        block.SwapSlot = -1;
    }

    private FileStream OpenSwapFile()
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.ReadWrite,
            Share = FileShare.None,
            Options = FileOptions.DeleteOnClose | FileOptions.RandomAccess
        };
        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        return new FileStream(Path.Combine(swapDirectory, $".aikernel-swap-{Guid.NewGuid():N}"), options);
    }

    private void TouchRam(Block block)
    {
        ram.Remove(block.Node);
        ram.AddLast(block.Node);
    }

    private void ValidateIndex(long blockIndex)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(blockIndex);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(blockIndex, BlockCount);
    }

    private void ValidateSize(int length, string parameter)
    {
        if (length != BlockSize)
        {
            throw new ArgumentException("The buffer must contain exactly one block.", parameter);
        }
    }

    private sealed class Block
    {
        internal Block() => Node = new LinkedListNode<Block>(this);

        internal LinkedListNode<Block> Node { get; }
        internal byte[]? Ram { get; set; }
        internal IVirtualMemoryDeviceBlock? Device { get; set; }
        internal long SwapSlot { get; set; } = -1;
        internal VirtualMemoryTier Tier => Ram is not null ? VirtualMemoryTier.Ram :
            Device is not null ? VirtualMemoryTier.Vram : VirtualMemoryTier.Swap;
    }
}
