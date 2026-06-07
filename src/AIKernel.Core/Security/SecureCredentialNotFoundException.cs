using System;
using System.Collections.Generic;
using System.Text;

namespace AIKernel.Core.Security;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Security.SecureCredentialNotFoundException']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Security.SecureCredentialNotFoundException']" />
public sealed class SecureCredentialNotFoundException(string key) : SecureCredentialException($"Secret was not found. Key='{key}'.")
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Security.SecureCredentialNotFoundException.Key']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Security.SecureCredentialNotFoundException.Key']" />
    public string Key { get; } = key;
}