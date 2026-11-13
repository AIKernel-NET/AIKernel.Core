using AIKernel.Kernel.Memory;

namespace AIKernel.Kernel.Inference;

// Owns one private CUDA context. The caller serializes all requests and disposal.
internal sealed unsafe class CudaQuantizedMatrix : IDisposable
{
    private readonly CudaVirtualMemoryDevice device;
    private readonly CudaVirtualMemoryDevice.DeviceBlock weights;
    internal long WeightBytes => weights.Length;
    internal string? PciBusId => device.GetPciBusId();

    internal CudaQuantizedMatrix(GgufWeights file, int ordinal, long maximumWeightBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ordinal);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumWeightBytes);
        if (file.Length > maximumWeightBytes || file.Length > int.MaxValue)
            throw new ArgumentException("Encoded model exceeds the configured GPU weight budget.");
        device = new(ordinal);
        try
        {
            weights = new(device, checked((int)file.Length));
            NativeCompute.Check(NativeCompute.DeviceAllocate(device.Context, (nuint)file.Length, out var address), "GPU model allocation");
            weights.Address = address;
            // Upload in bounded chunks: native staging never mirrors the entire model.
            const int chunkBytes = 1024 * 1024;
            for (long offset = 0; offset < file.Length; offset += chunkBytes)
            {
                var count = (int)Math.Min(chunkBytes, file.Length - offset);
                NativeCompute.Check(NativeCompute.Upload(device.Context, address + (ulong)offset,
                    file.BytesAt(offset, count), (nuint)count), "GPU model upload");
            }
            device.ZeroScratch();
        }
        catch { device.Dispose(); throw; }
    }

    internal void Multiply(GgufWeights.Tensor tensor, float[] input, float[] output, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        long bytes = checked((long)tensor.Rows * tensor.RowBytes);
        if (tensor.Rows < 1 || tensor.Columns < 1 || input.Length < tensor.Columns || output.Length < tensor.Rows ||
            tensor.Offset < 0 || tensor.Offset > WeightBytes || bytes > WeightBytes - tensor.Offset)
            throw new ArgumentException("GPU matrix dimensions or mapped range are invalid.");
        lock (device.Gate)
        {
            ObjectDisposedException.ThrowIf(weights.Address == 0, this);
            fixed (float* x = input)
            fixed (float* y = output)
            {
                ulong address = weights.Address + (ulong)tensor.Offset;
                int status = tensor.Type switch
                {
                    0 => NativeCompute.Gemv(device.Context, address, x, y, (uint)tensor.Rows, (uint)tensor.Columns),
                    41 => NativeCompute.GemvQ1(device.Context, address, x, y, (uint)tensor.Rows, (uint)tensor.Columns),
                    _ => throw new NotSupportedException("GPU matrix type is unsupported.")
                };
                NativeCompute.Check(status, "GPU matrix multiplication");
            }
        }
        token.ThrowIfCancellationRequested();
    }

    internal void ZeroScratch() => device.ZeroScratch();
    public void Dispose()
    {
        try { weights.Dispose(); }
        finally { device.Dispose(); }
    }
}
