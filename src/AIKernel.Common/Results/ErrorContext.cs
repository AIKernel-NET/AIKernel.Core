namespace AIKernel.Common.Results;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.ErrorContext']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.ErrorContext']" />
public sealed record ErrorContext(
    string Message,
    string Code,
    bool IsRetryable
)
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.ErrorContext.FailureKind']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.ErrorContext.FailureKind']" />
    public FailureKind? FailureKind { get; init; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.ErrorContext.OriginStep']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.ErrorContext.OriginStep']" />
    public OriginStep? OriginStep { get; init; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.ErrorContext.SemanticSlot']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.ErrorContext.SemanticSlot']" />
    public SemanticSlot? SemanticSlot { get; init; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.ErrorContext.string']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.ErrorContext.string']" />
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ErrorContext.FromException']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ErrorContext.FromException']" />
    public static ErrorContext FromException(Exception ex)
        => new(ex.Message, "UNHANDLED_EXCEPTION", false)
        {
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ResultMetadataKeys.ExceptionType] = ex.GetType().FullName ?? ex.GetType().Name
            }
        };

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ErrorContext.ToString']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ErrorContext.ToString']" />
    public override string ToString() => $"{Code}: {Message}";
}

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.FailureKind']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.FailureKind']" />
public enum FailureKind
{
    /// <summary>Gets the FailClosed value exposed by the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで公開される FailClosed 値を取得します。</summary>
    FailClosed,
    /// <summary>Gets the Reject value exposed by the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで公開される Reject 値を取得します。</summary>
    Reject,
    /// <summary>Gets the Quarantine value exposed by the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで公開される Quarantine 値を取得します。</summary>
    Quarantine
}

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.OriginStep']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.OriginStep']" />
public enum OriginStep
{
    /// <summary>Gets the Capability value exposed by the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで公開される Capability 値を取得します。</summary>
    Capability,
    /// <summary>Gets the Prompt value exposed by the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで公開される Prompt 値を取得します。</summary>
    Prompt,
    /// <summary>Gets the Provider value exposed by the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで公開される Provider 値を取得します。</summary>
    Provider,
    /// <summary>Gets the Tokenizer value exposed by the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで公開される Tokenizer 値を取得します。</summary>
    Tokenizer,
    /// <summary>Gets the SemanticHash value exposed by the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで公開される SemanticHash 値を取得します。</summary>
    SemanticHash,
    /// <summary>Gets the KernelFacade value exposed by the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで公開される KernelFacade 値を取得します。</summary>
    KernelFacade
}

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.SemanticSlot']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.SemanticSlot']" />
public enum SemanticSlot
{
    /// <summary>Gets the G value exposed by the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで公開される G 値を取得します。</summary>
    G,
    /// <summary>Gets the T value exposed by the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで公開される T 値を取得します。</summary>
    T,
    /// <summary>Gets the C value exposed by the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで公開される C 値を取得します。</summary>
    C,
    /// <summary>Gets the B value exposed by the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで公開される B 値を取得します。</summary>
    B
}
