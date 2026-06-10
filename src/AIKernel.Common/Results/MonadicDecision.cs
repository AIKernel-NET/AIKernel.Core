namespace AIKernel.Common.Results;

/// <summary>
/// [EN] Shared helpers for expressing small fail-closed decisions with AIKernel monads.
/// [JA] AIKernel monad で小さな fail-closed decision を表現するための共有 helper です。
/// </summary>
public static class MonadicDecision
{
    /// <summary>
    /// [EN] Converts a nullable reference into an option.
    /// [JA] nullable reference を option に変換します。
    /// </summary>
    public static Option<T> Optional<T>(T? value)
        where T : class
        => value is null
            ? Option<T>.None()
            : Option<T>.Some(value);

    /// <summary>
    /// [EN] Converts non-empty text into an option.
    /// [JA] 空ではない text を option に変換します。
    /// </summary>
    public static Option<string> OptionalText(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? Option<string>.None()
            : Option<string>.Some(value);

    /// <summary>
    /// [EN] Returns fallback text when the value is empty; otherwise returns the projected text.
    /// [JA] 値が空の場合は fallback text を返し、それ以外は projected text を返します。
    /// </summary>
    public static string TextOrDefault(
        string? value,
        string fallback,
        Func<string, string>? projector = null)
        => OptionalText(value).Match(
            () => fallback,
            text => projector is null ? text : projector(text));

    /// <summary>
    /// [EN] Returns an optional structured error when a condition is not satisfied.
    /// [JA] 条件が満たされない場合に optional structured error を返します。
    /// </summary>
    public static Option<MonadicError> ErrorUnless(
        bool condition,
        string code,
        string message)
        => condition
            ? Option<MonadicError>.None()
            : Option<MonadicError>.Some(new MonadicError(code, message));

    /// <summary>
    /// [EN] Creates a two-way decision that chooses the right branch when the condition is true.
    /// [JA] 条件が true の場合に right branch を選ぶ two-way decision を作成します。
    /// </summary>
    public static Either<TLeft, TRight> RightWhen<TLeft, TRight>(
        bool condition,
        TLeft left,
        TRight right)
        => condition
            ? Either<TLeft, TRight>.FromRight(right)
            : Either<TLeft, TRight>.FromLeft(left);

    /// <summary>
    /// [EN] Selects one of two text values through an Either decision.
    /// [JA] Either decision を通じて 2 つの text value のどちらかを選択します。
    /// </summary>
    public static string SelectText(
        bool condition,
        string left,
        string right)
        => RightWhen(condition, left, right).Match(value => value, value => value);

    /// <summary>
    /// [EN] Converts a success flag into a process-style exit code.
    /// [JA] success flag を process-style exit code に変換します。
    /// </summary>
    public static int ExitCode(bool succeeded)
        => RightWhen(succeeded, 1, 0).Match(value => value, value => value);
}

/// <summary>
/// [EN] Small immutable error value used by monadic decision helpers.
/// [JA] monadic decision helper が使用する小さな immutable error value です。
/// </summary>
public sealed record MonadicError(
    string Code,
    string Message);
