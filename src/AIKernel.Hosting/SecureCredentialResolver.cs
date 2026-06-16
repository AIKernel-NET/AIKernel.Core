namespace AIKernel.Hosting;

using AIKernel.Abstractions.Security;
using AIKernel.Core.Security;
using AIKernel.Core.Time;
using Microsoft.Extensions.Options;

/// <summary>EN: Documentation for public API. JA: SecureCredentialResolver を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Hosting.SecureCredentialResolver']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Hosting.SecureCredentialResolver']/summary" />
public sealed class SecureCredentialResolver<TOptions>(
    ISecureCredentialProvider credentialProvider,
    IOptions<TOptions> options,
    IKernelClock? clock = null)
    where TOptions : class, ISecureOptions
{
    private readonly ISecureCredentialProvider _credentialProvider = credentialProvider
            ?? throw new ArgumentNullException(nameof(credentialProvider));
    private readonly IOptions<TOptions> _options = options
            ?? throw new ArgumentNullException(nameof(options));
    private readonly IKernelClock _clock = clock ?? KernelClock.System();

    /// <summary>EN: Documentation for public API. JA: ResolveAsync を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Hosting.SecureCredentialResolver.ResolveAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Hosting.SecureCredentialResolver.ResolveAsync']/summary" />
    public async ValueTask<TOptions> ResolveAsync(
        CancellationToken cancellationToken = default)
    {
        var options = _options.Value;

        var hasApiKey = !string.IsNullOrWhiteSpace(options.ApiKey);
        var hasSecretKeyName = !string.IsNullOrWhiteSpace(options.SecretKeyName);

        if (!hasApiKey && !hasSecretKeyName)
        {
            throw new SecureCredentialNotFoundException(
                typeof(TOptions).Name);
        }

        if (hasApiKey && hasSecretKeyName)
        {
            throw new SecureCredentialAmbiguousException(
                $"Both ApiKey and SecretKeyName are specified for {typeof(TOptions).Name}.");
        }

        if (hasSecretKeyName)
        {
            var secretKeyName = options.SecretKeyName!;

            var secret = await _credentialProvider
                .GetSecretAsync(secretKeyName, cancellationToken)
                .ConfigureAwait(false);

            SecureCredentialGuard.ValidateSecret(
                secretKeyName,
                secret,
                expiresAtUtc: null,
                timeProvider: _clock.Logical);

            // This is the only mutation boundary.
            // Hosting injects the resolved secret into the secure option.
            options.ApiKey = secret;

            return options;
        }

        SecureCredentialGuard.ValidateSecret(
            $"{typeof(TOptions).Name}.ApiKey",
            options.ApiKey,
            expiresAtUtc: null,
            timeProvider: _clock.Logical);

        return options;
    }
}
