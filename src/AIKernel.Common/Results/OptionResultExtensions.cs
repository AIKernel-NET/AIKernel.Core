namespace AIKernel.Common.Results;

/// <summary>
/// [EN] Bridges optional presence checks into fail-closed Result pipelines.
/// [JA] optional な存在 check を fail-closed Result pipeline へ橋渡しします。
/// </summary>
public static class OptionResultExtensions
{
    /// <summary>
    /// [EN] Converts an option into a result using the supplied failure message when empty.
    /// [JA] 空の場合に指定された failure message を使って option を result に変換します。
    /// </summary>
    public static Result<T> AsResult<T>(this Option<T> option, string failureMessage)
        => option.HasValue
            ? Result<T>.Ok(option.Value!)
            : Result<T>.Fail(failureMessage);
}
