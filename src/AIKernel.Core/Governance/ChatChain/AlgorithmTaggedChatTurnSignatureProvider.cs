namespace AIKernel.Core.Governance.ChatChain;

using System.Text;
using AIKernel.Abstractions.Governance.ChatChain;

public sealed class AlgorithmTaggedChatTurnSignatureProvider : IChatTurnSignatureProvider
{
    public const string Algorithm = "aikernel-deterministic-signature-v1";

    public Task<string> SignAsync(
        string hash,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(hash ?? string.Empty));

        return Task.FromResult(Algorithm + ":" + payload);
    }

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
