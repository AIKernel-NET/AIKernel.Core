namespace AIKernel.Providers.MicrosoftAI;

public abstract class ProviderExecutionException : Exception
{
    protected ProviderExecutionException(string message)
        : base(message)
    {
    }

    protected ProviderExecutionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public abstract string ErrorCode { get; }
}

public sealed class ProviderInvalidResponseException : ProviderExecutionException
{
    public ProviderInvalidResponseException(string message)
        : base(message)
    {
    }

    public ProviderInvalidResponseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public override string ErrorCode => "invalid_provider_response";
}

public sealed class ProviderCapabilityMismatchException(string message) : ProviderExecutionException(message)
{
    public override string ErrorCode => "capability_mismatch";
}

public sealed class ProviderExecutionTimeoutException(string message, Exception innerException) : ProviderExecutionException(message, innerException)
{
    public override string ErrorCode => "provider_timeout";
}

public sealed class ProviderRateLimitException(string message, Exception innerException) : ProviderExecutionException(message, innerException)
{
    public override string ErrorCode => "rate_limited";
}

public sealed class ProviderApiException(string message, Exception innerException) : ProviderExecutionException(message, innerException)
{
    public override string ErrorCode => "provider_api_error";
}