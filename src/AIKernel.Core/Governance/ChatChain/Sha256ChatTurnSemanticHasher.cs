namespace AIKernel.Core.Governance.ChatChain;

using System.Security.Cryptography;
using System.Text;
using AIKernel.Abstractions.Governance.ChatChain;

/// <summary>EN: Documentation for public API. JA: Sha256ChatTurnSemanticHasher を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Governance.ChatChain.Sha256ChatTurnSemanticHasher']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Governance.ChatChain.Sha256ChatTurnSemanticHasher']/summary" />
public sealed class Sha256ChatTurnSemanticHasher : IChatTurnSemanticHasher
{
    /// <summary>EN: Documentation for public API. JA: Algorithm 定数を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Core.Governance.ChatChain.Sha256ChatTurnSemanticHasher.Algorithm']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Core.Governance.ChatChain.Sha256ChatTurnSemanticHasher.Algorithm']/summary" />
    public const string Algorithm = "sha256-chat-turn-v1";

    /// <summary>EN: Documentation for public API. JA: ComputeHash を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Governance.ChatChain.Sha256ChatTurnSemanticHasher.ComputeHash']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Governance.ChatChain.Sha256ChatTurnSemanticHasher.ComputeHash']/summary" />
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
