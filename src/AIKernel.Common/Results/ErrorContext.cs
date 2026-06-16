namespace AIKernel.Common.Results;

/// <summary>EN: Documentation for public API. JA: ErrorContext を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.ErrorContext']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.ErrorContext']/summary" />
public sealed record ErrorContext(
    string Message,
    string Code,
    bool IsRetryable
)
{
    /// <summary>EN: Documentation for public API. JA: FailureKind を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.ErrorContext.FailureKind']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.ErrorContext.FailureKind']/summary" />
    public FailureKind? FailureKind { get; init; }

    /// <summary>EN: Documentation for public API. JA: OriginStep を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.ErrorContext.OriginStep']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.ErrorContext.OriginStep']/summary" />
    public OriginStep? OriginStep { get; init; }

    /// <summary>EN: Documentation for public API. JA: SemanticSlot を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.ErrorContext.SemanticSlot']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.ErrorContext.SemanticSlot']/summary" />
    public SemanticSlot? SemanticSlot { get; init; }

    /// <summary>EN: Documentation for public API. JA: Metadata を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.ErrorContext.string']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.ErrorContext.string']/summary" />
    public IReadOnlyDictionary<string, string>? Metadata { get; init; }

    /// <summary>EN: Documentation for public API. JA: FromException を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ErrorContext.FromException']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ErrorContext.FromException']/summary" />
    public static ErrorContext FromException(Exception ex)
        => new(ex.Message, "UNHANDLED_EXCEPTION", false)
        {
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [ResultMetadataKeys.ExceptionType] = ex.GetType().FullName ?? ex.GetType().Name
            }
        };

    /// <summary>EN: Documentation for public API. JA: ToString を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ErrorContext.ToString']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.ErrorContext.ToString']/summary" />
    public override string ToString() => $"{Code}: {Message}";
}

/// <summary>EN: Documentation for public API. JA: FailureKind の値を定義します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.FailureKind']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.FailureKind']/summary" />
public enum FailureKind
{
    /// <summary>EN: Gets the FailClosed value exposed by the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで公開される FailClosed 値を取得します。</summary>
    FailClosed,
    /// <summary>EN: Gets the Reject value exposed by the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで公開される Reject 値を取得します。</summary>
    Reject,
    /// <summary>EN: Gets the Quarantine value exposed by the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで公開される Quarantine 値を取得します。</summary>
    Quarantine
}

/// <summary>EN: Documentation for public API. JA: OriginStep の値を定義します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.OriginStep']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.OriginStep']/summary" />
public enum OriginStep
{
    /// <summary>EN: Gets the Capability value exposed by the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで公開される Capability 値を取得します。</summary>
    Capability,
    /// <summary>EN: Gets the Prompt value exposed by the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで公開される Prompt 値を取得します。</summary>
    Prompt,
    /// <summary>EN: Gets the Provider value exposed by the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで公開される Provider 値を取得します。</summary>
    Provider,
    /// <summary>EN: Gets the Tokenizer value exposed by the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで公開される Tokenizer 値を取得します。</summary>
    Tokenizer,
    /// <summary>EN: Gets the SemanticHash value exposed by the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで公開される SemanticHash 値を取得します。</summary>
    SemanticHash,
    /// <summary>EN: Gets the KernelFacade value exposed by the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで公開される KernelFacade 値を取得します。</summary>
    KernelFacade
}

/// <summary>EN: Documentation for public API. JA: SemanticSlot の値を定義します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.SemanticSlot']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.SemanticSlot']/summary" />
public enum SemanticSlot
{
    /// <summary>EN: Gets the G value exposed by the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで公開される G 値を取得します。</summary>
    G,
    /// <summary>EN: Gets the T value exposed by the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで公開される T 値を取得します。</summary>
    T,
    /// <summary>EN: Gets the C value exposed by the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで公開される C 値を取得します。</summary>
    C,
    /// <summary>EN: Gets the B value exposed by the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで公開される B 値を取得します。</summary>
    B
}
