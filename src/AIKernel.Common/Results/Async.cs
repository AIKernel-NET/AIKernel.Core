namespace AIKernel.Common.Results;

/// <summary>
/// [EN] Represents an asynchronous fail-closed computation that returns a Result.
/// [JA] Result を返す非同期 fail-closed computation を表します。
/// </summary>
public readonly struct Async<T>
{
    private readonly Func<CancellationToken, Task<Result<T>>>? _run;

    private Async(Func<CancellationToken, Task<Result<T>>> run)
    {
        _run = run;
    }

    /// <summary>
    /// [EN] Creates an Async computation from a Result-producing function.
    /// [JA] Result 生成関数から Async computation を作成します。
    /// </summary>
    public static Async<T> From(Func<CancellationToken, Task<Result<T>>> run)
    {
        ArgumentNullException.ThrowIfNull(run);
        return new Async<T>(run);
    }

    /// <summary>
    /// [EN] Creates an Async computation from a successful value.
    /// [JA] 成功値から Async computation を作成します。
    /// </summary>
    public static Async<T> FromValue(T value)
        => From(_ => Result<T>.Success(value).AsTask());

    /// <summary>
    /// [EN] Creates an Async computation from an existing Result.
    /// [JA] 既存の Result から Async computation を作成します。
    /// </summary>
    public static Async<T> FromResult(Result<T> result)
        => From(_ => result.AsTask());

    /// <summary>
    /// [EN] Creates an Async computation from a Task-producing function and captures exceptions.
    /// [JA] Task 生成関数から Async computation を作成し、例外を捕捉します。
    /// </summary>
    public static Async<T> FromTask(Func<CancellationToken, Task<T>> task)
    {
        ArgumentNullException.ThrowIfNull(task);
        return From(cancellationToken => Try.RunAsync(() => task(cancellationToken)));
    }

    /// <summary>
    /// [EN] Executes the asynchronous computation.
    /// [JA] 非同期 computation を実行します。
    /// </summary>
    public Task<Result<T>> RunAsync(CancellationToken cancellationToken = default)
        => _run is null
            ? Result<T>.Fail("Async computation is not initialized. ErrorCode=ASYNC_NOT_INITIALIZED").AsTask()
            : _run(cancellationToken);

    /// <summary>
    /// [EN] Maps a successful asynchronous value.
    /// [JA] 成功した非同期値を map します。
    /// </summary>
    public Async<U> Select<U>(Func<T, U> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        var current = this;
        return Async<U>.From(async cancellationToken =>
        {
            var result = await current.RunAsync(cancellationToken).ConfigureAwait(false);
            return result.Map(selector);
        });
    }

    /// <summary>
    /// [EN] Binds a successful asynchronous value to another Async computation.
    /// [JA] 成功した非同期値を別の Async computation へ bind します。
    /// </summary>
    public Async<V> SelectMany<U, V>(Func<T, Async<U>> binder, Func<T, U, V> projector)
    {
        ArgumentNullException.ThrowIfNull(binder);
        ArgumentNullException.ThrowIfNull(projector);
        var current = this;
        return Async<V>.From(async cancellationToken =>
        {
            var first = await current.RunAsync(cancellationToken).ConfigureAwait(false);
            if (!first.IsSuccessState)
            {
                return Result<V>.Fail(first.Error!);
            }

            var second = await binder(first.Value).RunAsync(cancellationToken).ConfigureAwait(false);
            return second.Map(value => projector(first.Value, value));
        });
    }
}
