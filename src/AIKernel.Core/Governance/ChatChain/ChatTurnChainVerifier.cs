namespace AIKernel.Core.Governance.ChatChain;

using AIKernel.Abstractions.Governance.ChatChain;

public sealed class ChatTurnChainVerifier(
    IChatTurnCanonicalizer canonicalizer,
    IChatTurnSemanticHasher hasher,
    IChatTurnSignatureProvider signatureProvider) : IChatTurnChainVerifier
{
    private readonly IChatTurnCanonicalizer _canonicalizer =
        canonicalizer ?? throw new ArgumentNullException(nameof(canonicalizer));

    private readonly IChatTurnSemanticHasher _hasher =
        hasher ?? throw new ArgumentNullException(nameof(hasher));

    private readonly IChatTurnSignatureProvider _signatureProvider =
        signatureProvider ?? throw new ArgumentNullException(nameof(signatureProvider));

    public IChatTurnVerificationResult VerifyChain(IEnumerable<IHashChainNode> turns)
    {
        ArgumentNullException.ThrowIfNull(turns);

        var expectedPreviousHash = string.Empty;

        foreach (var turn in turns)
        {
            var result = VerifyNextTurn(turn, expectedPreviousHash);

            if (!result.IsSuccess)
            {
                return result;
            }

            expectedPreviousHash = turn.Hash;
        }

        return ChatTurnVerificationResult.Success;
    }

    public IChatTurnVerificationResult VerifyNextTurn(
        IHashChainNode nextTurn,
        string currentTailHash)
    {
        ArgumentNullException.ThrowIfNull(nextTurn);

        var expectedPreviousHash = currentTailHash ?? string.Empty;

        if (!string.Equals(nextTurn.PrevHash ?? string.Empty, expectedPreviousHash, StringComparison.Ordinal))
        {
            return ChatTurnVerificationResult.Fail("HASH_CHAIN_PREVIOUS_HASH_MISMATCH");
        }

        var canonical = _canonicalizer.Canonicalize(nextTurn.Turn);
        var expectedHash = _hasher.ComputeHash(canonical, expectedPreviousHash);

        if (!string.Equals(nextTurn.Hash, expectedHash, StringComparison.Ordinal))
        {
            return ChatTurnVerificationResult.Fail("HASH_CHAIN_HASH_MISMATCH");
        }

        var signature = nextTurn.Signature ?? string.Empty;

        if (!IsAlgorithmTagged(signature))
        {
            return ChatTurnVerificationResult.Fail("HASH_CHAIN_SIGNATURE_MISSING_ALGORITHM_TAG");
        }

        if (!VerifySignature(expectedHash, signature))
        {
            return ChatTurnVerificationResult.Fail("HASH_CHAIN_SIGNATURE_MISMATCH");
        }

        return ChatTurnVerificationResult.Success;
    }

    private bool VerifySignature(string expectedHash, string signature)
    {
        return _signatureProvider
            .VerifyAsync(expectedHash, signature, CancellationToken.None)
            .ConfigureAwait(false)
            .GetAwaiter()
            .GetResult();
    }

    private static bool IsAlgorithmTagged(string? signature)
    {
        if (string.IsNullOrWhiteSpace(signature))
        {
            return false;
        }

        var separatorIndex = signature.IndexOf(':', StringComparison.Ordinal);

        return separatorIndex > 0 && separatorIndex < signature.Length - 1;
    }
}
