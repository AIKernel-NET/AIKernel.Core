namespace AIKernel.Core.Governance.ChatChain;

using AIKernel.Abstractions.Governance.ChatChain;

public sealed record ChatTurnVerificationResult(
    bool IsSuccess,
    string Error) : IChatTurnVerificationResult
{
    public static ChatTurnVerificationResult Success { get; } = new(true, string.Empty);

    public static ChatTurnVerificationResult Fail(string error)
    {
        return new ChatTurnVerificationResult(false, error);
    }
}
