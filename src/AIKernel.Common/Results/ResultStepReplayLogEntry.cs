namespace AIKernel.Common.Results;

public sealed record ResultStepReplayLogEntry(
    string StepId,
    string? ParentStepId,
    SemanticDelta SemanticDelta,
    bool IsSuccess,
    string? ErrorCode);
