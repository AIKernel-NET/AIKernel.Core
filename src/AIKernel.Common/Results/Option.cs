namespace AIKernel.Common.Results;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.Option']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.Option']" />
public readonly struct Option<T>
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.Option.HasValue']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.Option.HasValue']" />
    public bool HasValue { get; }
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.Option.Value']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.Option.Value']" />
    public T? Value { get; }

    private Option(bool hasValue, T? value)
    {
        HasValue = hasValue;
        Value = value;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Option.Some']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Option.Some']" />
    public static Option<T> Some(T value)
        => new(true, value);

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Option.None']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Option.None']" />
    public static Option<T> None()
        => new(false, default);

    // -------------------------
    // Functional Extensions
    // -------------------------

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Option.Map&lt;U&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Option.Map&lt;U&gt;']" />
    public Option<U> Map<U>(Func<T, U> mapper)
        => HasValue
            ? Option<U>.Some(mapper(Value!))
            : Option<U>.None();

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Option.Bind&lt;U&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Option.Bind&lt;U&gt;']" />
    public Option<U> Bind<U>(Func<T, Option<U>> binder)
        => HasValue
            ? binder(Value!)
            : Option<U>.None();

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Option.Tap']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Option.Tap']" />
    public Option<T> Tap(Action<T> action)
    {
        if (HasValue)
        {
            action(Value!);
        }

        return this;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Option.OrElse']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Option.OrElse']" />
    public T OrElse(T fallback)
        => HasValue ? Value! : fallback;

    // -------------------------
    // LINQ Support
    // -------------------------

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Option.Select&lt;U&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Option.Select&lt;U&gt;']" />
    public Option<U> Select<U>(Func<T, U> selector)
        => Map(selector);

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Option.V&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Option.V&gt;']" />
    public Option<V> SelectMany<U, V>(
        Func<T, Option<U>> binder,
        Func<T, U, V> projector)
        => Bind(value => binder(value).Map(bound => projector(value, bound)));

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Option.ToString']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Option.ToString']" />
    public override string ToString()
        => HasValue ? $"Some({Value})" : "None";
}
