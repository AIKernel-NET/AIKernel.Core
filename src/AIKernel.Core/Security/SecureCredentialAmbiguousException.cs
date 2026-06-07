namespace AIKernel.Core.Security;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Security.SecureCredentialAmbiguousException']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Security.SecureCredentialAmbiguousException']" />
public sealed class SecureCredentialAmbiguousException(string message) : SecureCredentialException(message)
{
}