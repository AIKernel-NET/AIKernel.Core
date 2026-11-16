using System.Buffers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using AIKernel.Common.Results;
using AIKernel.Core.Memory;

namespace AIKernel.Kernel.Memory;

/// <summary>A logical address space backed by RAM, optional VRAM, and a private swap file.</summary>
public sealed partial class VirtualMemorySpace : IDisposable
{
    private readonly Arena arena;
    private readonly Dictionary<long, Block> blocks = [];
    private Lock gate => arena.Gate;
    private LinkedList<Block> ram => arena.Ram;
    private LinkedList<Block> vram => arena.Vram;
    private Stack<long> freeSlots => arena.FreeSlots;
    private Stack<IMemoryOwner<byte>> freeRam => arena.FreeRam;
    private IVirtualMemorySource? source => arena.Source;
    private int ramCapacity => arena.RamCapacity;
    private int vramCapacity => arena.VramCapacity;
    private IVirtualMemoryDevice? device => arena.Device;
    private long swapReads;
    private long backingReads;
    private long devicePageIns;
    private bool disposed;

    /// <summary>The number of addressable blocks.</summary>
    public long BlockCount { get; }

    /// <summary>The number of bytes in each block.</summary>
    public int BlockSize { get; }

    /// <summary>Creates a block address space with the default swap policy.</summary>
    public VirtualMemorySpace(long blockCount, int blockSize, int ramBlocks, string swapDirectory,
        int vramBlocks, IVirtualMemoryDevice? device, IVirtualMemorySource? source)
        : this(blockCount, blockSize, ramBlocks, swapDirectory, vramBlocks, device, source, default)
    {
    }

    /// <summary>
    /// Creates a block address space. Keep the optional device and external backing alive until all forks are closed.
    /// </summary>
    public VirtualMemorySpace(long blockCount, int blockSize, int ramBlocks, string swapDirectory,
        int vramBlocks = 0, IVirtualMemoryDevice? device = null, IVirtualMemorySource? source = null,
        SwapEncryptionPolicy swapPolicy = default)
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
            if (swapPolicy.ZeroWipe && device is not IZeroableVirtualMemoryDevice)
                throw new ArgumentException("The device must support zero-wipe.", nameof(device));
        }

        BlockCount = blockCount;
        BlockSize = blockSize;
        arena = new Arena(ramBlocks, vramBlocks, Path.GetFullPath(swapDirectory),
            vramBlocks > 0 ? device : null, source, swapPolicy);
    }

    private VirtualMemorySpace(VirtualMemorySpace parent)
    {
        BlockCount = parent.BlockCount;
        BlockSize = parent.BlockSize;
        arena = parent.arena;
        blocks.EnsureCapacity(parent.blocks.Count);
        foreach (var entry in parent.blocks)
            blocks.Add(entry.Key, entry.Value);
        if (arena.Spaces == int.MaxValue)
            throw new InvalidOperationException("Too many virtual memory forks.");
        foreach (var block in blocks.Values)
            block.RefCount++;
        arena.Spaces++;
    }

    /// <summary>Shares resident blocks and the tier budgets until a fork writes a page.</summary>
    public Result<VirtualMemorySpace> ForkSpace()
    {
        lock (gate)
        {
            if (disposed)
                return Fail<VirtualMemorySpace>("virtual_memory_closed", "The address space is closed.");
            try
            {
                return Result<VirtualMemorySpace>.Success(new VirtualMemorySpace(this));
            }
            catch (Exception ex) when (ex is OutOfMemoryException or InvalidOperationException)
            {
                return Fail<VirtualMemorySpace>("virtual_memory_fork_failed", ex.Message);
            }
        }
    }

    /// <summary>Reports how many spaces currently reference the materialized block.</summary>
    public int GetBlockReferenceCount(long blockIndex)
    {
        ValidateIndex(blockIndex);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return blocks.TryGetValue(blockIndex, out var block) ? block.RefCount : 0;
        }
    }

    /// <summary>Reports the physical tier capacity available across this arena's forks.</summary>
    public (int RamBlocks, int VramBlocks) GetAvailableBlocks()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            return (ramCapacity - ram.Count, vramCapacity - vram.Count);
        }
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
                if (block.RefCount > 1)
                {
                    ReplaceSharedBlock(block, CopyToRam(source));
                    return;
                }

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
            else if (block.RefCount > 1)
            {
                block = ReplaceSharedBlock(block, block.Ram is { } sharedRam ? CopyToRam(sharedRam.Memory.Span) : LoadBytes(block));
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
                !arena.BindOnAccess ? VirtualMemoryTier.Unallocated : VirtualMemoryTier.Swap;
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
            else if (block is { Ram: null, SwapSlot: < 0, HasBacking: true, Dirty: false } &&
                     source is SnapshotBacking snapshot)
            {
                operation(snapshot.GetPage(blockIndex), null);
                backingReads++;
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
                ReleaseReference(block);
                blocks.Remove(blockIndex);
            }
        }
    }

    private Block ReplaceSharedBlock(Block original, IMemoryOwner<byte> contents)
    {
        Block replacement;
        try
        {
            replacement = new Block(original.Index) { Dirty = true };
        }
        catch
        {
            ReturnRam(contents);
            throw;
        }

        InstallRam(replacement, contents);
        blocks[original.Index] = replacement;
        original.RefCount--;
        return replacement;
    }

    private void ReleaseReference(Block block)
    {
        if (block.RefCount == 1) ReleaseResidency(block);
        block.RefCount--;
    }

    /// <summary>Moves resident device blocks through RAM to make the device available to another execution.</summary>
    public Result<int> DemoteDeviceBlocks()
    {
        lock (gate)
        {
            if (disposed) return Fail<int>("virtual_memory_closed", "The address space is closed.");
            try
            {
                var moved = 0;
                foreach (var block in blocks.Values)
                {
                    if (block.Device is null) continue;
                    InstallRam(block, LoadBytes(block), allowVramEviction: false);
                    moved++;
                }
                return Result<int>.Success(moved);
            }
            catch (Exception ex) when (IsStorageFailure(ex))
            {
                return Fail<int>("virtual_memory_demotion_failed", ex.Message);
            }
        }
    }

    private Block? FindOrBind(long index)
    {
        if (blocks.TryGetValue(index, out var block) || !arena.BindOnAccess)
        {
            return block;
        }

        block = new Block(index) { HasBacking = true };
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
            var ramBlocks = 0;
            var vramBlocks = 0;
            foreach (var block in blocks.Values)
            {
                if (block.Ram is not null) ramBlocks++;
                if (block.Device is not null) vramBlocks++;
            }
            return new VirtualMemoryStatistics(ramBlocks, vramBlocks, blocks.Count - ramBlocks - vramBlocks,
                swapReads, devicePageIns) { BackingReads = backingReads };
        }
    }

    /// <summary>Evicts least-recently-used RAM pages across this arena's forks and releases spare buffers.
    /// Clean immutable pages return to their source; dirty pages use the configured swap policy.</summary>
    public void TrimRam(int maximumBlocks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumBlocks);
        lock(gate)
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            while(ram.Count>maximumBlocks)MoveToSwap(ram.First!.Value);
            while(freeRam.TryPop(out var owner))owner.Dispose();
        }
    }

    /// <summary>Changes the shared RAM capacity across forks, evicting before a shrink.
    /// A failed eviction preserves the previous capacity. Growth allocates on demand.</summary>
    public void SetRamCapacity(int maximumBlocks)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBlocks);
        lock(gate)
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            if(maximumBlocks==arena.RamCapacity)return;
            TrimRam(maximumBlocks);
            arena.RamCapacity=maximumBlocks;
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
            foreach (var block in blocks.Values)
            {
                try { ReleaseReference(block); }
                catch (Exception ex) { (errors ??= []).Add(ex); }
            }

            blocks.Clear();
            if (--arena.Spaces == 0)
            {
                // Best-effort cleanup of storage left after an earlier wipe or device error.
                foreach (var block in vram)
                {
                    try { ReleaseDevice(block.Device!); }
                    catch (Exception ex) { (errors ??= []).Add(ex); }
                }
                foreach (var block in ram) ReleaseHost(block.Ram!);
                while (freeRam.TryPop(out var owner)) ReleaseHost(owner);
                ram.Clear();
                vram.Clear();
                freeSlots.Clear();
                try { arena.Swap?.Dispose(); }
                catch (Exception ex) { (errors ??= []).Add(ex); }
                arena.OwnedSource?.Dispose();
            }
            if (arena.Policy.ZeroWipe && device is IZeroableVirtualMemoryDevice wipeDevice)
            {
                try { wipeDevice.ZeroScratch(); }
                catch (Exception ex) { (errors ??= []).Add(ex); }
            }
            if (errors is not null)
            {
                throw new AggregateException("One or more memory blocks could not be released.", errors);
            }

            void ReleaseHost(IMemoryOwner<byte> owner)
            {
                try
                {
                    if (arena.Policy.ZeroWipe) CryptographicOperations.ZeroMemory(owner.Memory.Span);
                    owner.Dispose();
                }
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

    private void InstallRam(Block block, IMemoryOwner<byte> bytes, bool allowVramEviction = true)
    {
        try
        {
            if (ram.Count == ramCapacity)
            {
                var victim = ram.First!.Value;
                if (!allowVramEviction || vramCapacity == 0 || !MoveToVram(victim, allowSwapFallback: true, incoming: block))
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
        if (arena.Policy.ZeroWipe) CryptographicOperations.ZeroMemory(owner.Memory.Span);
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
            if (arena.Policy.ZeroWipe && uploaded is not IZeroableDeviceBlock)
            {
                uploaded.Dispose();
                throw new InvalidOperationException("The device block does not support zero-wipe.");
            }
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
            ReleaseDevice(uploaded);
            throw;
        }

        block.Device = uploaded;
        vram.AddLast(block.Node);
        devicePageIns++;
        return true;
    }

    private void MoveToSwap(Block block)
    {
        if (block.HasBacking && !block.Dirty)
        {
            ReleaseResidency(block);
            return;
        }

        var swap = arena.Swap ??= OpenSwap();
        var temporary = block.Ram is null ? LoadBytes(block) : null;
        var slot = freeSlots.TryPop(out var available) ? available : checked(arena.NextSlot++);
        try
        {
            var version = swap.Write(slot, (block.Ram ?? temporary!).Memory.Span);
            ReleaseResidency(block);
            block.SwapVersion = version;
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
            else if (block.SwapSlot < 0 && block.HasBacking)
            {
                source!.ReadBlock(block.Index, bytes);
                backingReads++;
            }
            else
            {
                arena.Swap!.Read(block.SwapSlot, block.SwapVersion, bytes);
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
        if (block.Device is { } deviceBlock) ReleaseDevice(deviceBlock);
        if (block.Ram is { } owner) ReturnRam(owner);
        if (block.SwapSlot >= 0)
        {
            arena.Swap!.Clear(block.SwapSlot);
            freeSlots.Push(block.SwapSlot);
        }

        block.Node.List?.Remove(block.Node);
        block.Ram = null;
        block.Device = null;
        block.SwapSlot = -1;
    }

    private VirtualMemorySwap OpenSwap()
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

        var file = new FileStream(Path.Combine(arena.SwapDirectory, $".aikernel-swap-{Guid.NewGuid():N}"), options);
        try { return new VirtualMemorySwap(file, BlockSize, arena.Policy); }
        catch
        {
            file.Dispose();
            throw;
        }
    }

    private void ReleaseDevice(IVirtualMemoryDeviceBlock block)
    {
        if (arena.Policy.ZeroWipe)
        {
            if (block is not IZeroableDeviceBlock zeroable)
                throw new InvalidOperationException("The device block does not support zero-wipe.");
            zeroable.Zero();
        }
        block.Dispose();
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

    private sealed class Arena(int ramCapacity, int vramCapacity, string swapDirectory,
        IVirtualMemoryDevice? device, IVirtualMemorySource? source, SwapEncryptionPolicy policy)
    {
        internal Lock Gate { get; } = new();
        internal LinkedList<Block> Ram { get; } = new();
        internal LinkedList<Block> Vram { get; } = new();
        internal Stack<long> FreeSlots { get; } = new();
        internal Stack<IMemoryOwner<byte>> FreeRam { get; } = new(2);
        internal int RamCapacity { get; set; } = ramCapacity;
        internal int VramCapacity { get; } = vramCapacity;
        internal string SwapDirectory { get; } = swapDirectory;
        internal IVirtualMemoryDevice? Device { get; set; } = device;
        internal IVirtualMemorySource? Source { get; set; } = source;
        internal bool BindOnAccess { get; set; } = source is not null;
        internal SwapEncryptionPolicy Policy { get; } = policy;
        internal IDisposable? OwnedSource { get; set; }
        internal VirtualMemorySwap? Swap { get; set; }
        internal long NextSlot { get; set; }
        internal int Spaces { get; set; } = 1;
    }

    private sealed class Block
    {
        internal Block(long index)
        {
            Index = index;
            Node = new LinkedListNode<Block>(this);
        }

        internal long Index { get; }
        internal int RefCount { get; set; } = 1;
        internal bool HasBacking { get; init; }
        internal ulong SwapVersion { get; set; }
        internal bool Dirty { get; set; }

        internal LinkedListNode<Block> Node { get; }
        internal IMemoryOwner<byte>? Ram { get; set; }
        internal IVirtualMemoryDeviceBlock? Device { get; set; }
        internal long SwapSlot { get; set; } = -1;
        internal VirtualMemoryTier Tier => Ram is not null ? VirtualMemoryTier.Ram :
            Device is not null ? VirtualMemoryTier.Vram : VirtualMemoryTier.Swap;
    }
}
