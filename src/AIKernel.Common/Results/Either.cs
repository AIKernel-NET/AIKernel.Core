namespace AIKernel.Common.Results;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.Either']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.Either']" />
public readonly struct Either<L, R>
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.Either.IsRight']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.Either.IsRight']" />
    public bool IsRight { get; }
    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Common.Results.Either.IsLeft']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Common.Results.Either.IsLeft']" />
    public bool IsLeft => !IsRight;

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.Either.Left']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.Either.Left']" />
    public L? Left { get; }
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.Either.Right']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.Either.Right']" />
    public R? Right { get; }

    private Either(bool isRight, L? left, R? right)
    {
        IsRight = isRight;
        Left = left;
        Right = right;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Either.FromLeft']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Either.FromLeft']" />
    public static Either<L, R> FromLeft(L value)
        => new(false, value, default);

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Either.FromRight']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Either.FromRight']" />
    public static Either<L, R> FromRight(R value)
        => new(true, default, value);

    // -------------------------
    // Functional Extensions
    // -------------------------

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Either.Match&lt;T&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Either.Match&lt;T&gt;']" />
    public T Match<T>(Func<L, T> leftFunc, Func<R, T> rightFunc)
        => IsRight ? rightFunc(Right!) : leftFunc(Left!);

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Either.Map&lt;U&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Either.Map&lt;U&gt;']" />
    public Either<L, U> Map<U>(Func<R, U> mapper)
        => IsRight
            ? Either<L, U>.FromRight(mapper(Right!))
            : Either<L, U>.FromLeft(Left!);

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Either.Bind&lt;U&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Either.Bind&lt;U&gt;']" />
    public Either<L, U> Bind<U>(Func<R, Either<L, U>> binder)
        => IsRight
            ? binder(Right!)
            : Either<L, U>.FromLeft(Left!);

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Either.Tap']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Either.Tap']" />
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

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Either.Select&lt;U&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Either.Select&lt;U&gt;']" />
    public Either<L, U> Select<U>(Func<R, U> selector)
        => Map(selector);

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Either.V&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Either.V&gt;']" />
    public Either<L, V> SelectMany<U, V>(
        Func<R, Either<L, U>> binder,
        Func<R, U, V> projector)
        => Bind(value => binder(value).Map(bound => projector(value, bound)));

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Either.ToString']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Either.ToString']" />
    public override string ToString()
        => IsRight ? $"Right({Right})" : $"Left({Left})";
}
