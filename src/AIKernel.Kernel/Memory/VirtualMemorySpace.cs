using System.Buffers;
using System.Runtime.InteropServices;
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
    private readonly Stack<IMemoryOwner<byte>> freeRam = new(2);
    private readonly IVirtualMemorySource? source;
    private readonly string swapDirectory;
    private readonly int ramCapacity;
    private readonly int vramCapacity;
    private readonly IVirtualMemoryDevice? device;
    private FileStream? swapFile;
    private long nextSlot;
    private long swapReads;
    private long backingReads;
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
        int vramBlocks = 0, IVirtualMemoryDevice? device = null, IVirtualMemorySource? source = null)
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
        this.device = vramBlocks > 0 ? device : null;
        this.source = source;
    }

    /// <summary>Reads a whole block and promotes a resident copy to RAM on a page fault.</summary>
    public void ReadBlock(long blockIndex, Span<byte> destination)
    {
        ValidateIndex(blockIndex);
        ValidateSize(destination.Length, nameof(destination));
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var block = FindOrBind(blockIndex);
            if (block is null)
            {
                destination.Clear();
                return;
            }

            MoveToRam(block);
            block.Ram!.Memory.Span.CopyTo(destination);
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
                    source.CopyTo(bytes.Memory.Span);
                    block.Dirty = true;
                    TouchRam(block);
                    return;
                }

                InstallRam(block, CopyToRam(source));
                block.Dirty = true;
                return;
            }

            var created = new Block(blockIndex) { Dirty = true };
            blocks.EnsureCapacity(checked(blocks.Count + 1));
            InstallRam(created, CopyToRam(source));
            blocks.Add(blockIndex, created);
        }
    }

    /// <summary>Writes part of a block. Unwritten bytes in a new block are zero.</summary>
    public void WriteBlockSlice(long blockIndex, int offset, ReadOnlySpan<byte> bytes)
    {
        ValidateIndex(blockIndex);
        if (offset < 0 || bytes.Length > BlockSize - offset)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }

        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var block = FindOrBind(blockIndex);
            if (block is null)
            {
                block = new Block(blockIndex);
                blocks.EnsureCapacity(checked(blocks.Count + 1));
                var owner = RentRam();
                owner.Memory.Span.Clear();
                InstallRam(block, owner);
                blocks.Add(blockIndex, block);
            }
            else
            {
                MoveToRam(block);
            }

            bytes.CopyTo(block.Ram!.Memory.Span[offset..]);
            block.Dirty = true;
        }
    }

    /// <summary>Returns a block's current tier without changing its eviction order.</summary>
    public VirtualMemoryTier GetBlockTier(long blockIndex)
    {
        ValidateIndex(blockIndex);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return blocks.TryGetValue(blockIndex, out var block) ? block.Tier :
                source is null ? VirtualMemoryTier.Unallocated : VirtualMemoryTier.Swap;
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

            var block = FindOrBind(blockIndex);
            if (block is null || block.Tier == destination)
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
            var block = FindOrBind(blockIndex);
            if (vramCapacity == 0 || block is null)
            {
                throw new InvalidOperationException("A written block and a VRAM tier are required.");
            }

            EnsureDeviceResidency(block);
            operation(block.Device!);
        }
    }

    /// <summary>Resolves the block to the compute tier for the duration of a synchronous operation.</summary>
    /// <remarks>The callback borrows storage and must not retain it or reenter this address space.</remarks>
    public void UseComputeBlock(long blockIndex, VirtualMemoryBlockOperation operation)
    {
        ValidateIndex(blockIndex);
        ArgumentNullException.ThrowIfNull(operation);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var block = FindOrBind(blockIndex) ?? throw new InvalidOperationException("The block is unwritten.");
            if (vramCapacity > 0)
            {
                EnsureDeviceResidency(block);
                operation(default, block.Device);
            }
            else
            {
                MoveToRam(block);
                operation(block.Ram!.Memory.Span, null);
            }
        }
    }

    /// <summary>Discards a block and returns its RAM, VRAM, or swap slot to the allocator.</summary>
    public void DiscardBlock(long blockIndex)
    {
        ValidateIndex(blockIndex);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (blocks.TryGetValue(blockIndex, out var block))
            {
                ReleaseResidency(block);
                blocks.Remove(blockIndex);
            }
        }
    }

    private Block? FindOrBind(long index)
    {
        if (blocks.TryGetValue(index, out var block) || source is null)
        {
            return block;
        }

        block = new Block(index);
        blocks.Add(index, block);
        return block;
    }

    private void EnsureDeviceResidency(Block block)
    {
        if (block.Device is null)
        {
            MoveToVram(block, allowSwapFallback: false);
        }
        else
        {
            vram.Remove(block.Node);
            vram.AddLast(block.Node);
        }
    }

    /// <summary>Returns current residency and cumulative transfer counts.</summary>
    public VirtualMemoryStatistics GetStatistics()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return new VirtualMemoryStatistics(ram.Count, vram.Count, blocks.Count - ram.Count - vram.Count,
                swapReads, devicePageIns) { BackingReads = backingReads };
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

            foreach (var block in ram) ReleaseHost(block.Ram!);
            while (freeRam.TryPop(out var owner)) ReleaseHost(owner);
            blocks.Clear();
            ram.Clear();
            vram.Clear();
            freeSlots.Clear();
            swapFile?.Dispose();
            if (errors is not null)
            {
                throw new AggregateException("One or more memory blocks could not be released.", errors);
            }

            void ReleaseHost(IMemoryOwner<byte> owner)
            {
                try { owner.Dispose(); }
                catch (Exception ex) { (errors ??= []).Add(ex); }
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

    private void InstallRam(Block block, IMemoryOwner<byte> bytes)
    {
        try
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
        }
        catch
        {
            ReturnRam(bytes);
            throw;
        }

        block.Ram = bytes;
        ram.AddLast(block.Node);
    }

    private IMemoryOwner<byte> RentRam()
        => freeRam.TryPop(out var owner) ? owner :
            (device as IVirtualMemoryHostAllocator)?.AllocateHostBlock(BlockSize) ?? new HostBlock(BlockSize);

    private void ReturnRam(IMemoryOwner<byte> owner)
    {
        if (freeRam.Count < 2)
        {
            freeRam.Push(owner);
        }
        else
        {
            owner.Dispose();
        }
    }

    private IMemoryOwner<byte> CopyToRam(ReadOnlySpan<byte> sourceBytes)
    {
        var owner = RentRam();
        sourceBytes.CopyTo(owner.Memory.Span);
        return owner;
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

        var temporary = block.Ram is null ? LoadBytes(block) : null;
        var bytes = (block.Ram ?? temporary!).Memory.Span;
        IVirtualMemoryDeviceBlock uploaded;
        try
        {
            uploaded = device!.Upload(bytes);
        }
        catch (OutOfMemoryException) when (allowSwapFallback)
        {
            return false;
        }
        finally
        {
            if (temporary is not null) ReturnRam(temporary);
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
        if (source is not null && !block.Dirty)
        {
            ReleaseResidency(block);
            return;
        }

        var file = swapFile ??= OpenSwapFile();
        var temporary = block.Ram is null ? LoadBytes(block) : null;
        var slot = freeSlots.TryPop(out var available) ? available : nextSlot++;
        try
        {
            RandomAccess.Write(file.SafeFileHandle, (block.Ram ?? temporary!).Memory.Span, slot * BlockSize);
            ReleaseResidency(block);
        }
        catch
        {
            freeSlots.Push(slot);
            throw;
        }
        finally
        {
            if (temporary is not null) ReturnRam(temporary);
        }

        block.SwapSlot = slot;
    }

    private IMemoryOwner<byte> LoadBytes(Block block)
    {
        var owner = RentRam();
        try
        {
            var bytes = owner.Memory.Span;
            if (block.Device is { } deviceBlock)
            {
                deviceBlock.Download(bytes);
            }
            else if (block.SwapSlot < 0 && source is not null)
            {
                source.ReadBlock(block.Index, bytes);
                backingReads++;
            }
            else
            {
                var offset = block.SwapSlot * BlockSize;
                while (!bytes.IsEmpty)
                {
                    var read = RandomAccess.Read(swapFile!.SafeFileHandle, bytes, offset);
                    if (read == 0)
                    {
                        throw new EndOfStreamException("The swap file ended inside a block.");
                    }

                    bytes = bytes[read..];
                    offset += read;
                }

                swapReads++;
            }

            return owner;
        }
        catch
        {
            ReturnRam(owner);
            throw;
        }
    }

    private void ReleaseResidency(Block block)
    {
        block.Device?.Dispose();
        if (block.Ram is { } owner) ReturnRam(owner);
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

    private sealed unsafe class HostBlock(int length) : MemoryManager<byte>
    {
        private byte* address = (byte*)NativeMemory.Alloc((nuint)length);

        public override Span<byte> GetSpan()
        {
            ObjectDisposedException.ThrowIf(address == null, this);
            return new Span<byte>(address, length);
        }

        public override MemoryHandle Pin(int elementIndex = 0)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(elementIndex);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(elementIndex, length);
            ObjectDisposedException.ThrowIf(address == null, this);
            return new MemoryHandle(address + elementIndex);
        }

        public override void Unpin() { }

        protected override void Dispose(bool disposing)
        {
            NativeMemory.Free(address);
            address = null;
        }
    }

    private sealed class Block
    {
        internal Block(long index)
        {
            Index = index;
            Node = new LinkedListNode<Block>(this);
        }

        internal long Index { get; }
        internal bool Dirty { get; set; }

        internal LinkedListNode<Block> Node { get; }
        internal IMemoryOwner<byte>? Ram { get; set; }
        internal IVirtualMemoryDeviceBlock? Device { get; set; }
        internal long SwapSlot { get; set; } = -1;
        internal VirtualMemoryTier Tier => Ram is not null ? VirtualMemoryTier.Ram :
            Device is not null ? VirtualMemoryTier.Vram : VirtualMemoryTier.Swap;
    }
}
