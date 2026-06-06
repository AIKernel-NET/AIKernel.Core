namespace AIKernel.Common.Results;

public static class PipelineStepMetadataKeys
{
    public const string DeltaKind = "delta.kind";

    public const string LoopIteration = "loop_iteration";

    public const string LoopDecision = "loop_decision";

    public const string LoopTimestamp = "loop_timestamp";

    public const string SuspendReason = "suspend_reason";

    public const string ResumeReason = "resume_reason";

    public const string PreviousReplayLogCount = "previous_replay_log_count";

    public const string PreviousReplayLogHash = "previous_replay_log_hash";
}
