#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Dtos.Execution;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

public enum PromptOverflowPolicy
{
    FailClosed = 0,

    TruncateLowestPriorityContext = 1
}
