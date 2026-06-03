namespace AIKernel.Core.Dsl;

using AIKernel.Common.Results;

internal static class DslExecutionErrors
{
    public static ErrorContext InvalidRuntime(string message)
        => new(message, "DSL_RUNTIME_ERROR", false)
        {
            FailureKind = FailureKind.FailClosed,
            OriginStep = OriginStep.KernelFacade,
            SemanticSlot = SemanticSlot.T
        };
}
