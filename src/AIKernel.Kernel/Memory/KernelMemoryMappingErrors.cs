namespace AIKernel.Kernel.Memory;

using AIKernel.Common.Results;

internal static class KernelMemoryMappingErrors
{
    /// <summary>
    /// EN: Executes Error.
    /// EN: Documentation for public API. JA: Error を実行します。
    /// </summary>
    public static ErrorContext Error(string message)
        => new(message, "MEMORY_MAPPING_ERROR", false)
        {
            FailureKind = FailureKind.FailClosed,
            OriginStep = OriginStep.Capability,
            SemanticSlot = SemanticSlot.B
        };
    /// <summary>
    /// EN: Executes FromException.
    /// EN: Documentation for public API. JA: FromException を実行します。
    /// </summary>

    public static ErrorContext FromException(Exception exception)
        => Error(exception.Message) with
        {
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ResultMetadataKeys.ExceptionType] =
                    exception.GetType().FullName ?? exception.GetType().Name
            }
        };
}
