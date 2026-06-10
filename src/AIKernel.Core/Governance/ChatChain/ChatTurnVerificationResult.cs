namespace AIKernel.Core.Governance.ChatChain;

using AIKernel.Abstractions.Governance.ChatChain;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Governance.ChatChain.ChatTurnVerificationResult']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Governance.ChatChain.ChatTurnVerificationResult']/summary" />
public sealed record ChatTurnVerificationResult(
    bool IsSuccess,
    string Error) : IChatTurnVerificationResult
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Governance.ChatChain.ChatTurnVerificationResult.new']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Governance.ChatChain.ChatTurnVerificationResult.new']/summary" />
    public static ChatTurnVerificationResult Success { get; } = new(true, string.Empty);

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Governance.ChatChain.ChatTurnVerificationResult.Fail']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Governance.ChatChain.ChatTurnVerificationResult.Fail']/summary" />
    public static ChatTurnVerificationResult Fail(string error)
    {
        return new ChatTurnVerificationResult(false, error);
    }
}
