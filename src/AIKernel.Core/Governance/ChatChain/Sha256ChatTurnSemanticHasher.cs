namespace AIKernel.Core.Governance.ChatChain;

using System.Security.Cryptography;
using System.Text;
using AIKernel.Abstractions.Governance.ChatChain;

public sealed class Sha256ChatTurnSemanticHasher : IChatTurnSemanticHasher
{
    public const string Algorithm = "sha256-chat-turn-v1";

    public string ComputeHash(string canonicalContent, string previousHash)
    {
        var payload = string.Concat(
            previousHash ?? string.Empty,
            "\n",
            canonicalContent ?? string.Empty);

        var bytes = Encoding.UTF8.GetBytes(payload);
        var hash = SHA256.HashData(bytes);

        return Algorithm + ":" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
