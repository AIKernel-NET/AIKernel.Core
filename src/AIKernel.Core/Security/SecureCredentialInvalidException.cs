namespace AIKernel.Core.Security;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Security.SecureCredentialInvalidException']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Security.SecureCredentialInvalidException']" />
public sealed class SecureCredentialInvalidException(string key, string reason) : SecureCredentialException($"Secret is invalid. Key='{key}', Reason='{reason}'.")
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Security.SecureCredentialInvalidException.Key']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Security.SecureCredentialInvalidException.Key']" />
    public string Key { get; } = key;

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Security.SecureCredentialInvalidException.Reason']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Security.SecureCredentialInvalidException.Reason']" />
    public string Reason { get; } = reason;
}
