namespace AIKernel.Providers.MicrosoftAI;

using AIKernel.Core.Security;
using AIKernel.Core.Time;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.OpenAICompatibleCredential']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.OpenAICompatibleCredential']" />
public sealed record OpenAICompatibleCredential
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleCredential.ApiKey']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleCredential.ApiKey']" />
    public required string ApiKey { get; init; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleCredential.ExpiresAtUtc']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Providers.MicrosoftAI.OpenAICompatibleCredential.ExpiresAtUtc']" />
    public DateTimeOffset? ExpiresAtUtc { get; init; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleCredential.Create']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleCredential.Create']" />
    public static OpenAICompatibleCredential Create(
        string keyName,
        string apiKey,
        DateTimeOffset? expiresAtUtc,
        IKernelClock? clock = null)
    {
        SecureCredentialGuard.ValidateSecret(
            keyName,
            apiKey,
            expiresAtUtc,
            clock?.Logical);

        return new OpenAICompatibleCredential
        {
            ApiKey = apiKey,
            ExpiresAtUtc = expiresAtUtc
        };
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleCredential.ToString']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleCredential.ToString']" />
    public override string ToString()
    {
        return "OpenAICompatibleCredential { ApiKey = ***REDACTED*** }";
    }
}
