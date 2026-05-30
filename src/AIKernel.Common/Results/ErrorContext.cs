namespace AIKernel.Common.Results;

public sealed record ErrorContext(
    string Message,
    string Code,
    bool IsRetryable
)
{
    public static ErrorContext FromException(Exception ex)
        => new(ex.Message, "UNHANDLED_EXCEPTION", false);

    public override string ToString() => $"{Code}: {Message}";
}
