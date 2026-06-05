namespace AIKernel.Cuda.Libtorch.Cuda13.Model;

using System.Security.Cryptography;
using System.Text;
using AIKernel.Cuda.Libtorch.Cuda13.Interop;

public sealed record LlamaForwardResult(
    IReadOnlyList<int> OutputTokenIds,
    IReadOnlyList<float> Logits,
    string OutputHash)
{
    internal static LlamaForwardResult FromNative(
        ForwardResultNative native)
    {
        var outputTokens = (native.OutputTokenIds ?? [])
            .Take(Math.Clamp(native.OutputTokenCount, 0, 64))
            .ToArray();
        var logits = (native.Logits ?? [])
            .Take(Math.Clamp(native.LogitCount, 0, 4096))
            .ToArray();
        var material = string.Join(",", outputTokens) + "|" +
            string.Join(",", logits.Select(x => x.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));

        return new LlamaForwardResult(
            outputTokens,
            logits,
            Hash(material));
    }

    private static string Hash(
        string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return "sha256:" + Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
