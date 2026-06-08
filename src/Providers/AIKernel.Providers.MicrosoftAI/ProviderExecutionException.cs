namespace AIKernel.Providers.MicrosoftAI;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.ProviderExecutionException']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.ProviderExecutionException']" />
public abstract class ProviderExecutionException : Exception
{
    /// <summary>Initializes a new instance for the ProviderExecutionException AIKernel contract surface. JA: ProviderExecutionException AIKernel 契約サーフェスの新しいインスタンスを初期化します。</summary>
    protected ProviderExecutionException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance for the ProviderExecutionException AIKernel contract surface. JA: ProviderExecutionException AIKernel 契約サーフェスの新しいインスタンスを初期化します。</summary>
    protected ProviderExecutionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.ProviderExecutionException.string']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.ProviderExecutionException.string']" />
    public abstract string ErrorCode { get; }
}

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.ProviderInvalidResponseException']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.ProviderInvalidResponseException']" />
public sealed class ProviderInvalidResponseException : ProviderExecutionException
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.ProviderInvalidResponseException.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.ProviderInvalidResponseException.#ctor']" />
    public ProviderInvalidResponseException(string message)
        : base(message)
    {
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.ProviderInvalidResponseException.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.ProviderInvalidResponseException.#ctor']" />
    public ProviderInvalidResponseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.ProviderInvalidResponseException.ErrorCode']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.ProviderInvalidResponseException.ErrorCode']" />
    public override string ErrorCode => "invalid_provider_response";
}

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.ProviderCapabilityMismatchException']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.ProviderCapabilityMismatchException']" />
public sealed class ProviderCapabilityMismatchException(string message) : ProviderExecutionException(message)
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.ProviderCapabilityMismatchException.ErrorCode']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.ProviderCapabilityMismatchException.ErrorCode']" />
    public override string ErrorCode => "capability_mismatch";
}

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.ProviderExecutionTimeoutException']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.ProviderExecutionTimeoutException']" />
public sealed class ProviderExecutionTimeoutException(string message, Exception innerException) : ProviderExecutionException(message, innerException)
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.ProviderExecutionTimeoutException.ErrorCode']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.ProviderExecutionTimeoutException.ErrorCode']" />
    public override string ErrorCode => "provider_timeout";
}

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.ProviderRateLimitException']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.ProviderRateLimitException']" />
public sealed class ProviderRateLimitException(string message, Exception innerException) : ProviderExecutionException(message, innerException)
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.ProviderRateLimitException.ErrorCode']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.ProviderRateLimitException.ErrorCode']" />
    public override string ErrorCode => "rate_limited";
}

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.ProviderApiException']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.ProviderApiException']" />
public sealed class ProviderApiException(string message, Exception innerException) : ProviderExecutionException(message, innerException)
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.ProviderApiException.ErrorCode']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Providers.MicrosoftAI.ProviderApiException.ErrorCode']" />
    public override string ErrorCode => "provider_api_error";
}
