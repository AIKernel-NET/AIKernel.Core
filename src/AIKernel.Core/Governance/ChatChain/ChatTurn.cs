namespace AIKernel.Core.Governance.ChatChain;

using AIKernel.Abstractions.Governance.ChatChain;

public sealed record ChatTurn(
    string Actor,
    string Body,
    DateTime Timestamp) : IChatTurn;
