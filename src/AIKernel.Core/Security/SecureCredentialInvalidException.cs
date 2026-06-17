namespace AIKernel.Core.Security;

/// <summary>[EN] Documents this public package API member. [JA] SecureCredentialInvalidException を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Security.SecureCredentialInvalidException']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Security.SecureCredentialInvalidException']/summary" />
public sealed class SecureCredentialInvalidException(string key, string reason) : SecureCredentialException($"Secret is invalid. Key='{key}', Reason='{reason}'.")
{
    /// <summary>[EN] Documents this public package API member. [JA] Key を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Security.SecureCredentialInvalidException.Key']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Security.SecureCredentialInvalidException.Key']/summary" />
    public string Key { get; } = key;

    /// <summary>[EN] Documents this public package API member. [JA] Reason を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Security.SecureCredentialInvalidException.Reason']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Security.SecureCredentialInvalidException.Reason']/summary" />
    public string Reason { get; } = reason;
}
