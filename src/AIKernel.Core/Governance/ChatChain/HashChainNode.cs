namespace AIKernel.Core.Governance.ChatChain;

using AIKernel.Abstractions.Governance.ChatChain;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Governance.ChatChain.HashChainNode']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Governance.ChatChain.HashChainNode']/summary" />
public sealed record HashChainNode(
    IChatTurn Turn,
    string PrevHash,
    string Hash,
    string Signature) : IHashChainNode;
