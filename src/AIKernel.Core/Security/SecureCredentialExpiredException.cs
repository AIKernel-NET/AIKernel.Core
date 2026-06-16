namespace AIKernel.Core.Security;

/// <summary>EN: Documentation for public API. JA: SecureCredentialExpiredException を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Security.SecureCredentialExpiredException']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Security.SecureCredentialExpiredException']/summary" />
public sealed class SecureCredentialExpiredException(string key, DateTimeOffset expiresAtUtc) : SecureCredentialException($"Secret has expired. Key='{key}', ExpiresAtUtc='{expiresAtUtc:O}'.")
{
    /// <summary>EN: Documentation for public API. JA: Key を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Security.SecureCredentialExpiredException.Key']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Security.SecureCredentialExpiredException.Key']/summary" />
    public string Key { get; } = key;

    /// <summary>EN: Documentation for public API. JA: ExpiresAtUtc を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Security.SecureCredentialExpiredException.ExpiresAtUtc']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Security.SecureCredentialExpiredException.ExpiresAtUtc']/summary" />
    public DateTimeOffset ExpiresAtUtc { get; } = expiresAtUtc;
}
