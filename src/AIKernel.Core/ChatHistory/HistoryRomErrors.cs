namespace AIKernel.Core.ChatHistory;

using AIKernel.Common.Results;

internal static class HistoryRomErrors
{
    /// <summary>
    /// EN: Executes Error.
    /// EN: Documentation for public API. JA: Error を実行します。
    /// </summary>
    public static ErrorContext Error(string message)
        => new(message, "HISTORY_ROM_ERROR", false)
        {
            FailureKind = FailureKind.FailClosed,
            OriginStep = OriginStep.KernelFacade,
            SemanticSlot = SemanticSlot.C
        };
}
