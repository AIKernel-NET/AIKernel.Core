namespace AIKernel.Core.Governance.ChatChain;

using System.Text;
using AIKernel.Abstractions.Governance.ChatChain;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Governance.ChatChain.AlgorithmTaggedChatTurnSignatureProvider']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Governance.ChatChain.AlgorithmTaggedChatTurnSignatureProvider']" />
public sealed class AlgorithmTaggedChatTurnSignatureProvider : IChatTurnSignatureProvider
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Core.Governance.ChatChain.AlgorithmTaggedChatTurnSignatureProvider.Algorithm']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Core.Governance.ChatChain.AlgorithmTaggedChatTurnSignatureProvider.Algorithm']" />
    public const string Algorithm = "aikernel-deterministic-signature-v1";

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Governance.ChatChain.AlgorithmTaggedChatTurnSignatureProvider.SignAsync']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Governance.ChatChain.AlgorithmTaggedChatTurnSignatureProvider.SignAsync']" />
    public Task<string> SignAsync(
        string hash,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(hash ?? string.Empty));

        return Task.FromResult(Algorithm + ":" + payload);
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Governance.ChatChain.AlgorithmTaggedChatTurnSignatureProvider.VerifyAsync']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Governance.ChatChain.AlgorithmTaggedChatTurnSignatureProvider.VerifyAsync']" />
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
