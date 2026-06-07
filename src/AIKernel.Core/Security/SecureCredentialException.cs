namespace AIKernel.Core.Security;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Security.SecureCredentialException']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Security.SecureCredentialException']" />
public abstract class SecureCredentialException : Exception
{
    /// <summary>Initializes a new instance for the SecureCredentialException AIKernel contract surface. JA: SecureCredentialException AIKernel 契約サーフェスの新しいインスタンスを初期化します。</summary>
    protected SecureCredentialException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance for the SecureCredentialException AIKernel contract surface. JA: SecureCredentialException AIKernel 契約サーフェスの新しいインスタンスを初期化します。</summary>
    protected SecureCredentialException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
