namespace AIKernel.Common.Results;

public readonly struct Either<L, R>
{
    public bool IsRight { get; }
    public bool IsLeft => !IsRight;

    public L? Left { get; }
    public R? Right { get; }

    private Either(bool isRight, L? left, R? right)
    {
        IsRight = isRight;
        Left = left;
        Right = right;
    }

    public static Either<L, R> FromLeft(L value)
        => new(false, value, default);

    public static Either<L, R> FromRight(R value)
        => new(true, default, value);

    // -------------------------
    // Functional Extensions
    // -------------------------

    public T Match<T>(Func<L, T> leftFunc, Func<R, T> rightFunc)
        => IsRight ? rightFunc(Right!) : leftFunc(Left!);

    public Either<L, U> Map<U>(Func<R, U> mapper)
        => IsRight
            ? Either<L, U>.FromRight(mapper(Right!))
            : Either<L, U>.FromLeft(Left!);

    public Either<L, U> Bind<U>(Func<R, Either<L, U>> binder)
        => IsRight
            ? binder(Right!)
            : Either<L, U>.FromLeft(Left!);

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

    public Either<L, U> Select<U>(Func<R, U> selector)
        => Map(selector);

    public Either<L, V> SelectMany<U, V>(
        Func<R, Either<L, U>> binder,
        Func<R, U, V> projector)
        => Bind(value => binder(value).Map(bound => projector(value, bound)));

    public override string ToString()
        => IsRight ? $"Right({Right})" : $"Left({Left})";
}
