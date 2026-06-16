using System.Diagnostics.CodeAnalysis;

namespace AIKernel.Common.Results;

/// <summary>EN: Documentation for public API. JA: Result を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.Result']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.Result']/summary" />
public readonly struct Result<T>
{
    // -------------------------
    // Core State
    // -------------------------

    /// <summary>EN: Documentation for public API. JA: IsSuccess を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.Result.IsSuccess']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.Result.IsSuccess']/summary" />
    public bool IsSuccess { get; }

    /// <summary>EN: Documentation for public API. JA: IsFailure を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Common.Results.Result.IsFailure']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Common.Results.Result.IsFailure']/summary" />
    public bool IsFailure => !IsSuccess;

    /// <summary>EN: Documentation for public API. JA: Value を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.Result.Value']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.Result.Value']/summary" />
    public T? Value { get; }

    /// <summary>EN: Documentation for public API. JA: Error を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.Result.Error']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.Result.Error']/summary" />
    public ErrorContext? Error { get; }

    // Nullable Flow Analysis:
    // IsSuccess = true → Value は null ではない
    // IsSuccess = false → Error は null ではない
    /// <summary>
    /// [EN] Indicates success while providing nullable flow information for value and error access.
    /// [JA] value と error 参照の nullable flow 情報を提供しつつ success 状態を示します。
    /// </summary>
    [MemberNotNullWhen(true, nameof(Value))]
    [MemberNotNullWhen(false, nameof(Error))]
    public bool IsSuccessState => IsSuccess;

    private Result(bool success, T? value, ErrorContext? error)
    {
        IsSuccess = success;
        Value = value;
        Error = error;
    }

    // -------------------------
    // Constructors
    // -------------------------

    /// <summary>EN: Documentation for public API. JA: Success を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Result.Success']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Result.Success']/summary" />
    public static Result<T> Success(T value)
        => new(true, value, null);

    /// <summary>
    /// [EN] Creates a successful result using the short AIKernel monad guideline name.
    /// [JA] AIKernel monad guideline の短い名前で成功 result を作成します。
    /// </summary>
    public static Result<T> Ok(T value)
        => Success(value);

    /// <summary>EN: Documentation for public API. JA: Fail を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Result.Fail']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Result.Fail']/summary" />
    public static Result<T> Fail(string message)
        => new(false, default, new ErrorContext(message, "ERROR", false));

    /// <summary>
    /// [EN] Creates a failed result from an existing error context.
    /// [JA] 既存の error context から失敗 result を作成します。
    /// </summary>
    public static Result<T> Fail(ErrorContext error)
        => new(false, default, error);

    // -------------------------
    // Functional Extensions
    // -------------------------

    /// <summary>EN: Documentation for public API. JA: Map&lt;U&gt; を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Result.Map&lt;U&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Result.Map&lt;U&gt;']/summary" />
    public Result<U> Map<U>(Func<T, U> mapper)
    {
        if (IsFailure)
            return Result<U>.Fail(Error!);

        try
        {
            return Result<U>.Success(mapper(Value!));
        }
        catch (Exception ex)
        {
            return Result<U>.Fail(ErrorContext.FromException(ex));
        }
    }

    /// <summary>EN: Documentation for public API. JA: Bind&lt;U&gt; を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Result.Bind&lt;U&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Result.Bind&lt;U&gt;']/summary" />
    public Result<U> Bind<U>(Func<T, Result<U>> binder)
    {
        if (IsFailure)
            return Result<U>.Fail(Error!);

        try
        {
            return binder(Value!);
        }
        catch (Exception ex)
        {
            return Result<U>.Fail(ErrorContext.FromException(ex));
        }
    }

    /// <summary>EN: Documentation for public API. JA: Tap を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Result.Tap']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Result.Tap']/summary" />
    public Result<T> Tap(Action<T> action)
    {
        if (IsFailure)
            return this;

        try
        {
            action(Value!);
            return this;
        }
        catch (Exception ex)
        {
            return Fail(ErrorContext.FromException(ex));
        }
    }

    /// <summary>
    /// [EN] Returns this result when successful, otherwise evaluates a deterministic fallback.
    /// [JA] 成功時はこの result を返し、失敗時は決定論的 fallback を評価します。
    /// </summary>
    public Result<T> OrElse(Func<ErrorContext, Result<T>> fallback)
    {
        ArgumentNullException.ThrowIfNull(fallback);
        return IsSuccess ? this : fallback(Error!);
    }

    /// <summary>
    /// [EN] Returns this result when successful, otherwise returns a fallback result.
    /// [JA] 成功時はこの result を返し、失敗時は fallback result を返します。
    /// </summary>
    public Result<T> OrElse(Result<T> fallback)
        => IsSuccess ? this : fallback;

    /// <summary>
    /// [EN] Projects this result by evaluating the success branch or the fail-closed error branch.
    /// [JA] success branch または fail-closed error branch を評価して result を射影します。
    /// </summary>
    public U Match<U>(Func<ErrorContext, U> failFunc, Func<T, U> successFunc)
    {
        ArgumentNullException.ThrowIfNull(failFunc);
        ArgumentNullException.ThrowIfNull(successFunc);
        return IsSuccessState ? successFunc(Value) : failFunc(Error);
    }

    // -------------------------
    // LINQ Support
    // -------------------------

    /// <summary>EN: Documentation for public API. JA: Select&lt;U&gt; を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Result.Select&lt;U&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Result.Select&lt;U&gt;']/summary" />
    public Result<U> Select<U>(Func<T, U> selector)
        => Map(selector);

    /// <summary>EN: Documentation for public API. JA: V&gt; を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Result.V&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Result.V&gt;']/summary" />
    public Result<V> SelectMany<U, V>(
        Func<T, Result<U>> binder,
        Func<T, U, V> projector)
        => Bind(value => binder(value).Map(bound => projector(value, bound)));

    /// <summary>EN: Documentation for public API. JA: ToString を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Result.ToString']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Result.ToString']/summary" />
    public override string ToString()
        => IsSuccess ? $"Success({Value})" : $"Fail({Error})";
}
