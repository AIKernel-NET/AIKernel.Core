namespace AIKernel.Common.Exceptions;

/// <summary>[EN] Documents this public package API member. [JA] CommonException を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Exceptions.CommonException']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Exceptions.CommonException']/summary" />
public class CommonException : Exception
{
    /// <summary>[EN] Documents this public package API member. [JA] CommonException を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Exceptions.CommonException.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Exceptions.CommonException.#ctor']/summary" />
    public CommonException() { }

    /// <summary>
    /// [EN] Initializes a new exception instance with a message.
    /// [JA] メッセージを指定して新しい例外インスタンスを初期化します。
    /// </summary>
    public CommonException(string message) : base(message) { }

    /// <summary>
    /// [EN] Initializes a new exception instance with a message and inner exception.
    /// [JA] メッセージと内部例外を指定して新しい例外インスタンスを初期化します。
    /// </summary>
    public CommonException(string message, Exception inner) : base(message, inner) { }
}
