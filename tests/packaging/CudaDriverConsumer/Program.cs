using System.Runtime.InteropServices;
using AIKernel.Kernel.Memory;

// A null device explicitly requests host execution; no CUDA context or GPU is opened.
var compute = new TransformerCompute(null);
float[] values = [3, 1];
compute.Softmax(values);
Check(values[0], 1f / (1f + MathF.Exp(-2)));
Check(values[1], 1f / (1f + MathF.Exp(2)));
float[] weights = [1, 2, 3, 4, 5, 6];
float[] output = new float[2];
compute.MultiplyRows(MemoryMarshal.AsBytes(weights.AsSpan()), null, new float[] { 1, 2, 3 }, output);
Check(output[0], 14);
Check(output[1], 32);
Console.WriteLine($"cuda-driver-package: ok; rid={RuntimeInformation.RuntimeIdentifier}; mode=host; gpu-test=not-run");

static void Check(float actual, float expected)
{
    if (!float.IsFinite(actual) || MathF.Abs(actual - expected) > 1e-5f)
        throw new InvalidOperationException($"Native result mismatch: {actual} != {expected}.");
}
