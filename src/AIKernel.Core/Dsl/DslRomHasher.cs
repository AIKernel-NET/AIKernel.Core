namespace AIKernel.Core.Dsl;

using System.Security.Cryptography;
using System.Text;

internal static class DslRomHasher
{
    /// <summary>
    /// EN: Executes ComputeHash.
    /// EN: Documentation for public API. JA: ComputeHash を実行します。
    /// </summary>
    public static string ComputeHash(string jsonDsl)
    {
        ArgumentNullException.ThrowIfNull(jsonDsl);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(jsonDsl));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
