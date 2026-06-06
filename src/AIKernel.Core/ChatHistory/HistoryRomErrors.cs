namespace AIKernel.Core.ChatHistory;

using AIKernel.Common.Results;

internal static class HistoryRomErrors
{
    public static ErrorContext Error(string message)
        => new(message, "HISTORY_ROM_ERROR", false)
        {
            FailureKind = FailureKind.FailClosed,
            OriginStep = OriginStep.KernelFacade,
            SemanticSlot = SemanticSlot.C
        };
}
