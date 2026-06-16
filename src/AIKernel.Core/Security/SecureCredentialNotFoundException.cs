using System;
using System.Collections.Generic;
using System.Text;

namespace AIKernel.Core.Security;

/// <summary>EN: Documentation for public API. JA: SecureCredentialNotFoundException を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Security.SecureCredentialNotFoundException']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Security.SecureCredentialNotFoundException']/summary" />
public sealed class SecureCredentialNotFoundException(string key) : SecureCredentialException($"Secret was not found. Key='{key}'.")
{
    /// <summary>EN: Documentation for public API. JA: Key を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Security.SecureCredentialNotFoundException.Key']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Security.SecureCredentialNotFoundException.Key']/summary" />
    public string Key { get; } = key;
}