namespace AIKernel.Core.Execution;

using AIKernel.Abstractions.Providers;

public sealed record ModelMessage(
    string Role,
    string Content) : IModelMessage;
