namespace AIKernel.Core.Security;

using AIKernel.Abstractions.Security;
using AIKernel.Core.Time;

/// <summary>[EN] Documents this public package API member. [JA] EnvironmentCredentialProvider を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Security.EnvironmentCredentialProvider']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Security.EnvironmentCredentialProvider']/summary" />
public sealed class EnvironmentCredentialProvider(IKernelClock? clock = null) : ISecureCredentialProvider
{
    private readonly IKernelClock _clock = clock ?? KernelClock.System();

    /// <summary>[EN] Documents this public package API member. [JA] GetSecretAsync を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Security.EnvironmentCredentialProvider.GetSecretAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Security.EnvironmentCredentialProvider.GetSecretAsync']/summary" />
    public ValueTask<string> GetSecretAsync(
        string key,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new SecureCredentialInvalidException(
                "<empty>",
                "Environment variable name is required.");
        }

        var value = Environment.GetEnvironmentVariable(key);

        SecureCredentialGuard.ValidateSecret(
            key,
            value,
            expiresAtUtc: null,
            timeProvider: _clock.Logical);

        return ValueTask.FromResult(value!);
    }
}
