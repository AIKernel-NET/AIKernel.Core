namespace AIKernel.Common.Results;

/// <summary>EN: Documentation for public API. JA: Either を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.Either']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.Either']/summary" />
public readonly struct Either<L, R>
{
    /// <summary>EN: Documentation for public API. JA: IsRight を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.Either.IsRight']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.Either.IsRight']/summary" />
    public bool IsRight { get; }
    /// <summary>EN: Documentation for public API. JA: IsLeft を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Common.Results.Either.IsLeft']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Common.Results.Either.IsLeft']/summary" />
    public bool IsLeft => !IsRight;

    /// <summary>EN: Documentation for public API. JA: Left を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.Either.Left']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.Either.Left']/summary" />
    public L? Left { get; }
    /// <summary>EN: Documentation for public API. JA: Right を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.Either.Right']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.Either.Right']/summary" />
    public R? Right { get; }

    private Either(bool isRight, L? left, R? right)
    {
        IsRight = isRight;
        Left = left;
        Right = right;
    }

    /// <summary>EN: Documentation for public API. JA: FromLeft を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Either.FromLeft']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Either.FromLeft']/summary" />
    public static Either<L, R> FromLeft(L value)
        => new(false, value, default);

    /// <summary>EN: Documentation for public API. JA: FromRight を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Either.FromRight']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Either.FromRight']/summary" />
    public static Either<L, R> FromRight(R value)
        => new(true, default, value);

    // -------------------------
    // Functional Extensions
    // -------------------------

    /// <summary>EN: Documentation for public API. JA: Match&lt;T&gt; を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Either.Match&lt;T&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Either.Match&lt;T&gt;']/summary" />
    public T Match<T>(Func<L, T> leftFunc, Func<R, T> rightFunc)
        => IsRight ? rightFunc(Right!) : leftFunc(Left!);

    /// <summary>EN: Documentation for public API. JA: Map&lt;U&gt; を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Either.Map&lt;U&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Either.Map&lt;U&gt;']/summary" />
    public Either<L, U> Map<U>(Func<R, U> mapper)
        => IsRight
            ? Either<L, U>.FromRight(mapper(Right!))
            : Either<L, U>.FromLeft(Left!);

    /// <summary>EN: Documentation for public API. JA: Bind&lt;U&gt; を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Either.Bind&lt;U&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Either.Bind&lt;U&gt;']/summary" />
    public Either<L, U> Bind<U>(Func<R, Either<L, U>> binder)
        => IsRight
            ? binder(Right!)
            : Either<L, U>.FromLeft(Left!);

    /// <summary>EN: Documentation for public API. JA: Tap を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Either.Tap']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Either.Tap']/summary" />
    public Either<L, R> Tap(Action<R> action)
    {
        if (IsRight)
        {
            action(Right!);
        }

        return this;
    }

    // -------------------------
    // LINQ Support
    // -------------------------

    /// <summary>EN: Documentation for public API. JA: Select&lt;U&gt; を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Either.Select&lt;U&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Either.Select&lt;U&gt;']/summary" />
    public Either<L, U> Select<U>(Func<R, U> selector)
        => Map(selector);

    /// <summary>EN: Documentation for public API. JA: V&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Either.V&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Either.V&gt;']/summary" />
    public Either<L, V> SelectMany<U, V>(
        Func<R, Either<L, U>> binder,
        Func<R, U, V> projector)
        => Bind(value => binder(value).Map(bound => projector(value, bound)));

    /// <summary>EN: Documentation for public API. JA: ToString を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Either.ToString']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Either.ToString']/summary" />
    public override string ToString()
        => IsRight ? $"Right({Right})" : $"Left({Left})";
}
