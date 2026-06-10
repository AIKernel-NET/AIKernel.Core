namespace AIKernel.Common.Results;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.ResultStepReplayLogEntry']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.ResultStepReplayLogEntry']/summary" />
public sealed record ResultStepReplayLogEntry(
    string StepId,
    string? ParentStepId,
    SemanticDelta SemanticDelta,
    bool IsSuccess,
    string? ErrorCode);
