namespace AIKernel.Core.Security;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Security.SecureCredentialInvalidException']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Security.SecureCredentialInvalidException']/summary" />
public sealed class SecureCredentialInvalidException(string key, string reason) : SecureCredentialException($"Secret is invalid. Key='{key}', Reason='{reason}'.")
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Security.SecureCredentialInvalidException.Key']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Security.SecureCredentialInvalidException.Key']/summary" />
    public string Key { get; } = key;

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Security.SecureCredentialInvalidException.Reason']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Security.SecureCredentialInvalidException.Reason']/summary" />
    public string Reason { get; } = reason;
}
