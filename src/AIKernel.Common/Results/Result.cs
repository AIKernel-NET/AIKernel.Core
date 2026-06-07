using System.Diagnostics.CodeAnalysis;

namespace AIKernel.Common.Results;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.Result']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.Result']" />
public readonly struct Result<T>
{
    // -------------------------
    // Core State
    // -------------------------

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.Result.IsSuccess']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.Result.IsSuccess']" />
    public bool IsSuccess { get; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Common.Results.Result.IsFailure']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Common.Results.Result.IsFailure']" />
    public bool IsFailure => !IsSuccess;

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.Result.Value']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.Result.Value']" />
    public T? Value { get; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.Result.Error']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Common.Results.Result.Error']" />
    public ErrorContext? Error { get; }

    // Nullable Flow Analysis:
    // IsSuccess = true → Value は null ではない
    // IsSuccess = false → Error は null ではない
    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Common.Results.Result.IsSuccessState']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Common.Results.Result.IsSuccessState']" />
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

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Result.Success']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Result.Success']" />
    public static Result<T> Success(T value)
        => new(true, value, null);

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Result.Fail']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Result.Fail']" />
    public static Result<T> Fail(string message)
        => new(false, default, new ErrorContext(message, "ERROR", false));

    /// <summary>Executes the Fail operation on the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで Fail 操作を実行します。</summary>
    public static Result<T> Fail(ErrorContext error)
        => new(false, default, error);

    // -------------------------
    // Functional Extensions
    // -------------------------

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Result.Map&lt;U&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Result.Map&lt;U&gt;']" />
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

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Result.Bind&lt;U&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Result.Bind&lt;U&gt;']" />
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

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Result.Tap']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Result.Tap']" />
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

    // -------------------------
    // LINQ Support
    // -------------------------

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Result.Select&lt;U&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Result.Select&lt;U&gt;']" />
    public Result<U> Select<U>(Func<T, U> selector)
        => Map(selector);

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Result.V&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Result.V&gt;']" />
    public Result<V> SelectMany<U, V>(
        Func<T, Result<U>> binder,
        Func<T, U, V> projector)
        => Bind(value => binder(value).Map(bound => projector(value, bound)));

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Result.ToString']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.Result.ToString']" />
    public override string ToString()
        => IsSuccess ? $"Success({Value})" : $"Fail({Error})";
}
