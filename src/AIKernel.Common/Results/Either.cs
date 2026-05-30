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

    // -------------------------
    // LINQ Support
    // -------------------------

    public Either<L, U> Select<U>(Func<R, U> selector)
        => IsRight
            ? Either<L, U>.FromRight(selector(Right!))
            : Either<L, U>.FromLeft(Left!);

    public Either<L, V> SelectMany<U, V>(
        Func<R, Either<L, U>> binder,
        Func<R, U, V> projector)
    {
        if (IsLeft)
            return Either<L, V>.FromLeft(Left!);

        var r = binder(Right!);
        if (r.IsLeft)
            return Either<L, V>.FromLeft(r.Left!);

        return Either<L, V>.FromRight(projector(Right!, r.Right!));
    }

    public override string ToString()
        => IsRight ? $"Right({Right})" : $"Left({Left})";
}
