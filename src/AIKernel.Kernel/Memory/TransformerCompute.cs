using AIKernel.Core.Memory;

namespace AIKernel.Kernel.Memory;

/// <summary>FP32 Transformer primitives over the same C ABI on CUDA and the host.</summary>
public sealed unsafe class TransformerCompute(CudaVirtualMemoryDevice? device)
{
    private readonly Lock hostGate = new();
    private Lock Gate => device?.Gate ?? hostGate;
    private nint Context => device?.Context ?? 0;

    /// <summary>Multiplies rows of a resident weight block by an input vector.</summary>
    public void MultiplyRows(ReadOnlySpan<byte> host, IVirtualMemoryDeviceBlock? resident, ReadOnlySpan<float> input,
        Span<float> output)
    {
        var count = (long)input.Length * output.Length;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        lock (Gate)
        {
            fixed (byte* matrix = host)
            fixed (float* x = input, y = output)
            {
                var context = Context;
                var address = (ulong)(nuint)matrix;
                if (context != 0)
                {
                    if (resident is not CudaVirtualMemoryDevice.DeviceBlock block ||
                        !ReferenceEquals(block.Owner, device) || count > block.Length / sizeof(float))
                    {
                        throw new ArgumentException("Invalid CUDA weight block.", nameof(resident));
                    }

                    ObjectDisposedException.ThrowIf(block.Address == 0, block);
                    address = block.Address;
                }
                else if (count > host.Length / sizeof(float) || resident is not null)
                {
                    throw new ArgumentException("Invalid host weight block.", nameof(host));
                }

                NativeCompute.Check(NativeCompute.Gemv(context, address, x, y, (uint)output.Length,
                    (uint)input.Length), "GEMV");
            }
        }
    }

    /// <summary>Applies RMS normalization or mean-centered layer normalization.</summary>
    public void Normalize(ReadOnlySpan<float> input, ReadOnlySpan<float> scale, Span<float> output,
        float epsilon = 1e-5f, bool center = false)
    {
        ValidateLength(input.Length, scale.Length);
        ValidateLength(input.Length, output.Length);
        lock (Gate)
        {
            fixed (float* x = input, w = scale, y = output)
            {
                NativeCompute.Check(NativeCompute.Normalize(Context, x, w, y, (uint)input.Length,
                    epsilon, center ? 1 : 0), "Normalization");
            }
        }
    }

    /// <summary>Rotates input pairs by the supplied interleaved sine and cosine coefficients.</summary>
    public void Rope(Span<float> input, ReadOnlySpan<float> sinCos)
    {
        ValidateLength(input.Length, sinCos.Length);
        lock (Gate)
        {
            fixed (float* x = input, frequencies = sinCos)
            {
                NativeCompute.Check(NativeCompute.Rope(Context, x, frequencies, (uint)input.Length), "RoPE");
            }
        }
    }

    /// <summary>Applies SiLU to the gate and multiplies it by the up projection in place.</summary>
    public void SwiGlu(Span<float> gate, ReadOnlySpan<float> up)
    {
        ValidateLength(gate.Length, up.Length);
        lock (Gate)
        {
            fixed (float* g = gate, u = up)
            {
                NativeCompute.Check(NativeCompute.SwiGlu(Context, g, u, (uint)gate.Length), "SwiGLU");
            }
        }
    }

    /// <summary>Applies stable softmax in place.</summary>
    public void Softmax(Span<float> values)
    {
        lock (Gate)
        {
            fixed (float* p = values)
            {
                NativeCompute.Check(NativeCompute.Softmax(Context, p, (uint)values.Length), "Softmax");
            }
        }
    }

    /// <summary>Computes scores or weighted values from one resident KV page.</summary>
    public void AttentionTile(ReadOnlySpan<byte> host, IVirtualMemoryDeviceBlock? resident,
        ReadOnlySpan<float> input, Span<float> output, int rows, int kvWidth, int headOffset, int headWidth, bool values)
    {
        ValidateLength(values ? rows : headWidth, input.Length);
        ValidateLength(values ? headWidth : rows, output.Length);
        var bytes = (long)rows * kvWidth * 2 * sizeof(float);
        lock (Gate)
        {
            fixed (byte* page = host)
            fixed (float* x = input, y = output)
            {
                var context = 0L;
                var address = (ulong)(nuint)page;
                if (resident is CudaVirtualMemoryDevice.DeviceBlock block && ReferenceEquals(block.Owner, device))
                {
                    ObjectDisposedException.ThrowIf(block.Address == 0, block);
                    if (bytes > block.Length) throw new ArgumentException("KV rows exceed the device block.");
                    address = block.Address;
                    context = Context;
                }
                else if (resident is not null || bytes > host.Length)
                {
                    throw new ArgumentException("Invalid host KV block.");
                }

                NativeCompute.Check(NativeCompute.AttentionTile((nint)context, address, x, y, (uint)rows,
                    (uint)kvWidth, (uint)headOffset, (uint)headWidth, values ? 1u : 0u), "Attention tile");
            }
        }
    }

    private static void ValidateLength(int expected, int actual)
    {
        if (expected != actual)
        {
            throw new ArgumentException("Vector lengths must match.");
        }
    }
}
