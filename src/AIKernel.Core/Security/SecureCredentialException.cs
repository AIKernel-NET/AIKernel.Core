namespace AIKernel.Core.Security;

/// <summary>EN: Documentation for public API. JA: SecureCredentialException を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Security.SecureCredentialException']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Security.SecureCredentialException']/summary" />
public abstract class SecureCredentialException : Exception
{
    /// <summary>EN: Initializes a new instance for the SecureCredentialException AIKernel contract surface. JA: SecureCredentialException AIKernel 契約サーフェスの新しいインスタンスを初期化します。</summary>
    protected SecureCredentialException(string message)
        : base(message)
    {
    }

    /// <summary>EN: Initializes a new instance for the SecureCredentialException AIKernel contract surface. JA: SecureCredentialException AIKernel 契約サーフェスの新しいインスタンスを初期化します。</summary>
    protected SecureCredentialException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
