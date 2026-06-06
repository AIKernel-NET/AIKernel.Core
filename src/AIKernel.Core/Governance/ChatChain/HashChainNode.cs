namespace AIKernel.Core.Governance.ChatChain;

using AIKernel.Abstractions.Governance.ChatChain;

public sealed record HashChainNode(
    IChatTurn Turn,
    string PrevHash,
    string Hash,
    string Signature) : IHashChainNode;
