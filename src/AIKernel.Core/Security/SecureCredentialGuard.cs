namespace AIKernel.Core.Security;

using AIKernel.Core.Time;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Security.SecureCredentialGuard']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Security.SecureCredentialGuard']/summary" />
public static class SecureCredentialGuard
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Security.SecureCredentialGuard.ValidateSecret']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Security.SecureCredentialGuard.ValidateSecret']/summary" />
    public static void ValidateSecret(
        string key,
        string? value,
        DateTimeOffset? expiresAtUtc = null,
        KernelTimeProvider? timeProvider = null)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new SecureCredentialInvalidException(
                "<empty>",
                "Secret key name is required.");
        }

        if (value is null)
        {
            throw new SecureCredentialNotFoundException(key);
        }

        if (value.Length == 0)
        {
            throw new SecureCredentialInvalidException(
                key,
                "Secret value must not be empty.");
        }

        if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new SecureCredentialInvalidException(
                key,
                "Secret value must not contain leading or trailing whitespace.");
        }

        if (value.Any(char.IsControl))
        {
            throw new SecureCredentialInvalidException(
                key,
                "Secret value must not contain control characters.");
        }

        if (value.Length < 8)
        {
            throw new SecureCredentialInvalidException(
                key,
                "Secret value is too short.");
        }

        if (expiresAtUtc is not null)
        {
            var now = (timeProvider ?? KernelClock.System().Logical).GetUtcNow();

            if (expiresAtUtc.Value <= now)
            {
                throw new SecureCredentialExpiredException(
                    key,
                    expiresAtUtc.Value);
            }
        }
    }
}
