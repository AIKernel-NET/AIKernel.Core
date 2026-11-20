using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using AIKernel.Kernel.Memory;
using Microsoft.Win32.SafeHandles;

namespace AIKernel.Kernel.Inference;

internal readonly record struct CosmosWeightPage(SafeFileHandle File, long Offset, int Bytes);

// The caller serializes page visits and resizing. A span is valid only inside its callback.
internal interface ICosmosWeightPages : IDisposable
{
    int Capacity { get; }
    long ResidentBytes { get; }
    long BackingReads { get; }
    void Resize(int capacity);
    void Trim();
    void Visit(int page, bool scan, VirtualMemoryBlockOperation operation);
}

internal sealed class BufferedCosmosWeightPages(VirtualMemorySpace space) : ICosmosWeightPages
{
    public long ResidentBytes => (long)space.GetStatistics().RamBlocks * space.BlockSize;
    public int Capacity => space.GetAvailableBlocks().RamBlocks + space.GetStatistics().RamBlocks;
    public long BackingReads => space.GetStatistics().BackingReads;
    public void Resize(int capacity) => space.SetRamCapacity(capacity);
    public void Trim() => space.TrimRam(0);
    public void Visit(int page, bool scan, VirtualMemoryBlockOperation operation) => space.UseComputeBlock(page, operation);
    public void Dispose() => space.Dispose();
}

// Only immutable weight bytes use file views. KV and request data keep their existing swap policy.
[SupportedOSPlatform("windows6.2")]
internal sealed unsafe partial class WindowsCosmosWeightPages(IReadOnlyList<CosmosWeightPage> pages) : ICosmosWeightPages
{
    private readonly Dictionary<SafeFileHandle, MemoryMappedFile> files = [];
    private readonly Dictionary<int, LinkedListNode<Window>> resident = [];
    private readonly LinkedList<Window> order = new();
    public int Capacity { get; private set; } = 1;
    public long ResidentBytes { get; private set; }
    public long BackingReads { get; private set; }

    private sealed record Window(int Page, MemoryMappedViewAccessor View);

    public void Visit(int page, bool scan, VirtualMemoryBlockOperation operation)
    {
        var source = pages[page];
        bool miss = !resident.TryGetValue(page, out var node);
        if (node is null)
        {
            if (order.Count == Capacity) Evict();
            if (!files.TryGetValue(source.File, out var file))
            {
                file = MemoryMappedFile.CreateFromFile(source.File, null, 0, MemoryMappedFileAccess.Read,
                    HandleInheritability.None, leaveOpen: true);
                files.Add(source.File, file);
            }
            var view = file.CreateViewAccessor(source.Offset, source.Bytes, MemoryMappedFileAccess.Read);
            node = order.AddLast(new Window(page, view));
            resident.Add(page, node);
            ResidentBytes += source.Bytes;
            BackingReads++;
        }
        else
        {
            order.Remove(node);
            order.AddLast(node);
        }

        var accessor = node.Value.View;
        var handle = accessor.SafeMemoryMappedViewHandle;
        byte* pointer = null;
        handle.AcquirePointer(ref pointer);
        try
        {
            pointer += accessor.PointerOffset;
            if (miss && scan)
            {
                // A contiguous matrix scan consumes this entire window. Sparse row reads fault on demand.
                var range = new MemoryRange { Address = pointer, Length = (nuint)source.Bytes };
                _ = PrefetchVirtualMemory(-1, 1, &range, 0);
            }
            operation(new ReadOnlySpan<byte>(pointer, source.Bytes), null);
        }
        finally { handle.ReleasePointer(); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryRange { internal void* Address; internal nuint Length; }

    [LibraryImport("kernel32.dll")]
    private static partial int PrefetchVirtualMemory(nint process, nuint count, MemoryRange* ranges, uint flags);

    private void Evict()
    {
        var window = order.First!.Value;
        order.RemoveFirst();
        resident.Remove(window.Page);
        ResidentBytes -= pages[window.Page].Bytes;
        window.View.Dispose();
    }

    public void Resize(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        while (order.Count > capacity) Evict();
        Capacity = capacity;
    }

    public void Trim() { while (order.Count > 0) Evict(); }
    public void Dispose()
    {
        Trim();
        foreach (var file in files.Values) file.Dispose();
        files.Clear();
    }
}
