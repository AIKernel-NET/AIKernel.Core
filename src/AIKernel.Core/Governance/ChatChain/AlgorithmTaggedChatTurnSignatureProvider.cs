namespace AIKernel.Core.Governance.ChatChain;

using System.Text;
using AIKernel.Abstractions.Governance.ChatChain;

/// <summary>EN: Documentation for public API. JA: AlgorithmTaggedChatTurnSignatureProvider を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Governance.ChatChain.AlgorithmTaggedChatTurnSignatureProvider']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Governance.ChatChain.AlgorithmTaggedChatTurnSignatureProvider']/summary" />
public sealed class AlgorithmTaggedChatTurnSignatureProvider : IChatTurnSignatureProvider
{
    /// <summary>EN: Documentation for public API. JA: Algorithm 定数を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Core.Governance.ChatChain.AlgorithmTaggedChatTurnSignatureProvider.Algorithm']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Core.Governance.ChatChain.AlgorithmTaggedChatTurnSignatureProvider.Algorithm']/summary" />
    public const string Algorithm = "aikernel-deterministic-signature-v1";

    /// <summary>EN: Documentation for public API. JA: SignAsync を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Governance.ChatChain.AlgorithmTaggedChatTurnSignatureProvider.SignAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Governance.ChatChain.AlgorithmTaggedChatTurnSignatureProvider.SignAsync']/summary" />
    public Task<string> SignAsync(
        string hash,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(hash ?? string.Empty));

        return Task.FromResult(Algorithm + ":" + payload);
    }

    /// <summary>EN: Documentation for public API. JA: VerifyAsync を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Governance.ChatChain.AlgorithmTaggedChatTurnSignatureProvider.VerifyAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Governance.ChatChain.AlgorithmTaggedChatTurnSignatureProvider.VerifyAsync']/summary" />
    public async Task<bool> VerifyAsync(
        string hash,
        string signature,
        CancellationToken cancellationToken)
    {
        var expected = await SignAsync(hash, cancellationToken)
            .ConfigureAwait(false);

        return string.Equals(expected, signature, StringComparison.Ordinal);
    }
}
