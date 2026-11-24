using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace AIKernel.Kernel.Inference;

/// <summary>Both readers import row-major FP32 into the same private, request-independent backing.</summary>
internal sealed unsafe class SnapshotMatrixStore : IDisposable
{
    private readonly FileStream file;
    private MemoryMappedFile? mapping;
    private MemoryMappedViewAccessor? view;
    private byte* address;
    internal TensorPageStore? pages;
    private readonly List<ModelTensor> tensors = [];
    internal long Length => file.Length;
    internal long? Reads => pages?.BackingBytesRead;
    internal long? Capacity => pages?.CacheCapacityBytes;

    internal SnapshotMatrixStore()
    {
        string path = Path.Combine(Path.GetTempPath(),Path.GetRandomFileName());
        file = CreateBacking(path);
    }
    private static FileStream CreateBacking(string path)
    {
        var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.ReadWrite,
            Share = FileShare.None, Options = FileOptions.DeleteOnClose };
        if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        return new FileStream(path, options);
    }
    internal ModelTensor Add(TensorSlice slice)
    {
        var tensor = new ModelTensor(file.Position, slice.Rows, slice.Columns, checked(slice.Columns * 4), "F32", 1, 4);
        slice.WriteTo(file);
        tensors.Add(tensor);
        return tensor;
    }
    internal void Seal(GgufMemoryOptions? budget)
    {
        file.Flush();
        if (budget is not null)
        {
            pages = budget.OpenSnapshot(file, tensors);
            return;
        }
        Map();
    }
    private void Map()
    {
        mapping = MemoryMappedFile.CreateFromFile(file, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, true);
        view = mapping.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read);
        view.SafeMemoryMappedViewHandle.AcquirePointer(ref address);
    }
    internal void ReadRow(ModelTensor tensor, int row, float[] output)
    {
        if (pages is not null) pages.VisitRow(tensor, row, b => MemoryMarshal.Cast<byte, float>(b).CopyTo(output));
        else new ReadOnlySpan<float>(address + tensor.Backing + (tensor.FirstRow + (long)row) * tensor.RowBytes, tensor.Columns).CopyTo(output);
    }
    internal void Multiply(ModelTensor tensor, float[] input, float[] output, ParallelOptions options)
    {
        if (pages is null)
        {
            Rows((nint)(address + tensor.Backing + (long)tensor.FirstRow * tensor.RowBytes), 0, tensor.Rows);
            return;
        }
        pages.VisitRows(tensor, options.CancellationToken, Rows);
        void Rows(nint baseAddress, int first, int count) => MultiplyRows(baseAddress, first, count, tensor.Columns, input, output, options);
    }
    private static void MultiplyRows(nint address,int first,int count,int width,float[] input,float[] output,ParallelOptions options) =>
        Parallel.For(0,count,options,r=>output[first+r]=InferenceVectorMath.Dot(
            new ReadOnlySpan<float>((float*)address+(long)r*width,width),input.AsSpan(0,width)));
    public void Dispose()
    {
        if (address != null) { view!.SafeMemoryMappedViewHandle.ReleasePointer(); address = null; }
        try { pages?.Dispose(); } finally { view?.Dispose(); mapping?.Dispose(); file.Dispose(); }
    }
}
