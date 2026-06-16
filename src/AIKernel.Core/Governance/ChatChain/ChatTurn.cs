namespace AIKernel.Core.Governance.ChatChain;

using AIKernel.Abstractions.Governance.ChatChain;

/// <summary>EN: Documentation for public API. JA: ChatTurn を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Governance.ChatChain.ChatTurn']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Governance.ChatChain.ChatTurn']/summary" />
public sealed record ChatTurn(
    string Actor,
    string Body,
    DateTime Timestamp) : IChatTurn;
