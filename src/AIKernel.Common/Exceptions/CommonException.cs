namespace AIKernel.Common.Exceptions;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Exceptions.CommonException']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Exceptions.CommonException']" />
public class CommonException : Exception
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Exceptions.CommonException.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Exceptions.CommonException.#ctor']" />
    public CommonException() { }

    /// <summary>Initializes a new instance for the CommonException AIKernel contract surface. JA: CommonException AIKernel 契約サーフェスの新しいインスタンスを初期化します。</summary>
    public CommonException(string message) : base(message) { }

    /// <summary>Initializes a new instance for the CommonException AIKernel contract surface. JA: CommonException AIKernel 契約サーフェスの新しいインスタンスを初期化します。</summary>
    public CommonException(string message, Exception inner) : base(message, inner) { }
}
