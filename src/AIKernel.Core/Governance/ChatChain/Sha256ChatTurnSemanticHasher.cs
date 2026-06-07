namespace AIKernel.Core.Governance.ChatChain;

using System.Security.Cryptography;
using System.Text;
using AIKernel.Abstractions.Governance.ChatChain;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Governance.ChatChain.Sha256ChatTurnSemanticHasher']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Governance.ChatChain.Sha256ChatTurnSemanticHasher']" />
public sealed class Sha256ChatTurnSemanticHasher : IChatTurnSemanticHasher
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Core.Governance.ChatChain.Sha256ChatTurnSemanticHasher.Algorithm']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Core.Governance.ChatChain.Sha256ChatTurnSemanticHasher.Algorithm']" />
    public const string Algorithm = "sha256-chat-turn-v1";

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Governance.ChatChain.Sha256ChatTurnSemanticHasher.ComputeHash']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Governance.ChatChain.Sha256ChatTurnSemanticHasher.ComputeHash']" />
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
