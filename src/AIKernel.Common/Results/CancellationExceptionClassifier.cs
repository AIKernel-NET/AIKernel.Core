namespace AIKernel.Common.Results;

/// <summary>
/// Identifies cancellation from an exception's runtime type at an execution boundary.
/// 実行境界で例外の実型からキャンセルを判定します。
/// </summary>
public static class CancellationExceptionClassifier
{
    /// <summary>
    /// Returns true for OperationCanceledException and all its derived types, including TaskCanceledException.
    /// OperationCanceledException と TaskCanceledException を含む派生型に対して true を返します。
    /// </summary>
    /// <remarks>
    /// Token identity or state is not required: a linked or internal token can represent cancellation.
    /// Ordinary failures, timeouts and wrappers are not cancellation solely because their caller is canceled
    /// or an inner exception represents cancellation. No exception metadata or type-name strings are trusted.
    /// リンク済み・内部トークンも扱い、通常の失敗やタイムアウトを呼出元の取消状態だけで再分類しません。
    /// </remarks>
    /// <param name="exception">The original exception, or null. 元の例外、または null。</param>
    /// <returns>Whether the exception represents cancellation. キャンセルを表す例外かどうか。</returns>
    public static bool IsCancellation(Exception? exception)
        => exception is OperationCanceledException;
}
