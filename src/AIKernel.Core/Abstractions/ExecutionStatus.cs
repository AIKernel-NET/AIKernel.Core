#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Dtos.Execution;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

public enum ExecutionStatus
{
    Succeeded = 0,

    Rejected = 1,

    Failed = 2,

    Canceled = 3,

    TimedOut = 4,

    RateLimited = 5
}
