namespace AIKernel.Providers.MicrosoftAI;

using Microsoft.Extensions.Options;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptionsValidator']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptionsValidator']" />
public sealed class OpenAICompatibleProviderOptionsValidator
    : IValidateOptions<OpenAICompatibleProviderOptions>
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptionsValidator.Validate']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Providers.MicrosoftAI.OpenAICompatibleProviderOptionsValidator.Validate']" />
    public ValidateOptionsResult Validate(
        string? name,
        OpenAICompatibleProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ProviderId))
        {
            failures.Add("ProviderId is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Name))
        {
            failures.Add("Name is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Version))
        {
            failures.Add("Version is required.");
        }

        if (string.IsNullOrWhiteSpace(options.ModelId))
        {
            failures.Add("ModelId is required.");
        }

        if (options.MaxInputTokens <= 0)
        {
            failures.Add("MaxInputTokens must be greater than zero.");
        }

        if (options.MaxOutputTokens is <= 0)
        {
            failures.Add("MaxOutputTokens must be greater than zero when specified.");
        }

        var hasDirectApiKey = !string.IsNullOrWhiteSpace(options.ApiKey);
        var hasSecretKeyName = !string.IsNullOrWhiteSpace(options.SecretKeyName);

        if (!hasDirectApiKey && !hasSecretKeyName)
        {
            failures.Add("Either ApiKey or SecretKeyName must be specified.");
        }

        if (hasDirectApiKey && options.ApiKey is not null)
        {
            if (!string.Equals(options.ApiKey, options.ApiKey.Trim(), StringComparison.Ordinal))
            {
                failures.Add("ApiKey must not contain leading or trailing whitespace.");
            }

            if (options.ApiKey.Any(char.IsControl))
            {
                failures.Add("ApiKey must not contain control characters.");
            }

            if (options.ApiKey.Length < 8)
            {
                failures.Add("ApiKey is too short.");
            }
        }

        if (hasDirectApiKey && hasSecretKeyName)
        {
            failures.Add("Do not specify both ApiKey and SecretKeyName in configuration.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
