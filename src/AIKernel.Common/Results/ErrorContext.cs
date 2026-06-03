namespace AIKernel.Common.Results;

public sealed record ErrorContext(
    string Message,
    string Code,
    bool IsRetryable
)
{
    public FailureKind? FailureKind { get; init; }

    public OriginStep? OriginStep { get; init; }

    public SemanticSlot? SemanticSlot { get; init; }

    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    public static ErrorContext FromException(Exception ex)
        => new(ex.Message, "UNHANDLED_EXCEPTION", false)
        {
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ResultMetadataKeys.ExceptionType] = ex.GetType().FullName ?? ex.GetType().Name
            }
        };

    public override string ToString() => $"{Code}: {Message}";
}

public enum FailureKind
{
    FailClosed,
    Reject,
    Quarantine
}

public enum OriginStep
{
    Capability,
    Prompt,
    Provider,
    Tokenizer,
    SemanticHash,
    KernelFacade
}

public enum SemanticSlot
{
    G,
    T,
    C,
    B
}
