namespace AIKernel.Core.Security;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Security.SecureCredentialExpiredException']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Security.SecureCredentialExpiredException']" />
public sealed class SecureCredentialExpiredException(string key, DateTimeOffset expiresAtUtc) : SecureCredentialException($"Secret has expired. Key='{key}', ExpiresAtUtc='{expiresAtUtc:O}'.")
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Security.SecureCredentialExpiredException.Key']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Security.SecureCredentialExpiredException.Key']" />
    public string Key { get; } = key;

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Security.SecureCredentialExpiredException.ExpiresAtUtc']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Security.SecureCredentialExpiredException.ExpiresAtUtc']" />
    public DateTimeOffset ExpiresAtUtc { get; } = expiresAtUtc;
}
