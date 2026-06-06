namespace AIKernel.Core.Dsl;

using System.Security.Cryptography;
using System.Text;

public static class DslRomHasher
{
    public static string ComputeHash(string jsonDsl)
    {
        ArgumentNullException.ThrowIfNull(jsonDsl);

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(jsonDsl));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
