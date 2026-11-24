using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace AIKernel.Kernel.Inference;

internal static class GeneratedTokenDigest
{
    internal static string Compute(List<int> tokens) =>
        Convert.ToHexStringLower(SHA256.HashData(MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(tokens))));
}
