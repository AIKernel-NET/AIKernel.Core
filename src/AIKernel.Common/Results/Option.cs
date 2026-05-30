namespace AIKernel.Common.Results;

public readonly struct Option<T>
{
    public bool HasValue { get; }
    public T? Value { get; }

    private Option(bool hasValue, T? value)
    {
        HasValue = hasValue;
        Value = value;
    }

    public static Option<T> Some(T value)
        => new(true, value);

    public static Option<T> None()
        => new(false, default);

    // -------------------------
    // Functional Extensions
    // -------------------------

    public Option<U> Map<U>(Func<T, U> mapper)
        => HasValue
            ? Option<U>.Some(mapper(Value!))
            : Option<U>.None();

    public T OrElse(T fallback)
        => HasValue ? Value! : fallback;

    // -------------------------
    // LINQ Support
    // -------------------------

    public Option<U> Select<U>(Func<T, U> selector)
        => Map(selector);

    public Option<V> SelectMany<U, V>(
        Func<T, Option<U>> binder,
        Func<T, U, V> projector)
    {
        if (!HasValue)
            return Option<V>.None();

        var r = binder(Value!);
        if (!r.HasValue)
            return Option<V>.None();

        return Option<V>.Some(projector(Value!, r.Value!));
    }

    public override string ToString()
        => HasValue ? $"Some({Value})" : "None";
}
