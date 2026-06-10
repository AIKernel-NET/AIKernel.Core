namespace AIKernel.Common.Results;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.Option']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.Option']/summary" />
public readonly struct Option<T>
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.Option.HasValue']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.Option.HasValue']/summary" />
    public bool HasValue { get; }
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.Option.Value']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.Option.Value']/summary" />
    public T? Value { get; }

    private Option(bool hasValue, T? value)
    {
        HasValue = hasValue;
        Value = value;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Option.Some']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Option.Some']/summary" />
    public static Option<T> Some(T value)
        => new(true, value);

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Option.None']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Option.None']/summary" />
    public static Option<T> None()
        => new(false, default);

    // -------------------------
    // Functional Extensions
    // -------------------------

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Option.Map&lt;U&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Option.Map&lt;U&gt;']/summary" />
    public Option<U> Map<U>(Func<T, U> mapper)
        => HasValue
            ? Option<U>.Some(mapper(Value!))
            : Option<U>.None();

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Option.Bind&lt;U&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Option.Bind&lt;U&gt;']/summary" />
    public Option<U> Bind<U>(Func<T, Option<U>> binder)
        => HasValue
            ? binder(Value!)
            : Option<U>.None();

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Option.Tap']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Option.Tap']/summary" />
    public Option<T> Tap(Action<T> action)
    {
        if (HasValue)
        {
            action(Value!);
        }

        return this;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Option.OrElse']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Option.OrElse']/summary" />
    public T OrElse(T fallback)
        => HasValue ? Value! : fallback;

    /// <summary>
    /// [EN] Projects the option by evaluating the some branch when a value exists, otherwise the none branch.
    /// [JA] 値が存在する場合は some branch、存在しない場合は none branch を評価して option を射影します。
    /// </summary>
    public U Match<U>(Func<U> noneFunc, Func<T, U> someFunc)
    {
        ArgumentNullException.ThrowIfNull(noneFunc);
        ArgumentNullException.ThrowIfNull(someFunc);
        return HasValue ? someFunc(Value!) : noneFunc();
    }

    // -------------------------
    // LINQ Support
    // -------------------------

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Option.Select&lt;U&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Option.Select&lt;U&gt;']/summary" />
    public Option<U> Select<U>(Func<T, U> selector)
        => Map(selector);

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Option.V&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Option.V&gt;']/summary" />
    public Option<V> SelectMany<U, V>(
        Func<T, Option<U>> binder,
        Func<T, U, V> projector)
        => Bind(value => binder(value).Map(bound => projector(value, bound)));

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Option.ToString']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Option.ToString']/summary" />
    public override string ToString()
        => HasValue ? $"Some({Value})" : "None";
}
