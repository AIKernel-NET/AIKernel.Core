using AIKernel.Dtos.Vfs;
using AIKernel.Vfs;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIKernel.Core.Vfs.Abstractions;

internal sealed class VfsQueryResultSnapshot : IVfsQueryResult
{
    /// <summary>
    /// EN: Gets IsSuccessful.
    /// [EN] Documents this public package API member. [JA] IsSuccessful を取得します。
    /// </summary>
    public required bool IsSuccessful { get; init; }
    /// <summary>
    /// EN: Gets RowCount.
    /// [EN] Documents this public package API member. [JA] RowCount を取得します。
    /// </summary>

    public required int RowCount { get; init; }
    /// <summary>
    /// EN: Gets ColumnNames.
    /// [EN] Documents this public package API member. [JA] ColumnNames を取得します。
    /// </summary>

    public required IReadOnlyList<string> ColumnNames { get; init; }
    /// <summary>
    /// EN: Gets Rows.
    /// [EN] Documents this public package API member. [JA] Rows を取得します。
    /// </summary>

    public required IReadOnlyList<VfsQueryRow> Rows { get; init; }
    /// <summary>
    /// EN: Gets ErrorMessage.
    /// [EN] Documents this public package API member. [JA] ErrorMessage を取得します。
    /// </summary>

    public string? ErrorMessage { get; init; }
    /// <summary>
    /// EN: Executes Failure.
    /// [EN] Documents this public package API member. [JA] Failure を実行します。
    /// </summary>

    public static VfsQueryResultSnapshot Failure(string errorMessage) => new()
    {
        IsSuccessful = false,
        RowCount = 0,
        ColumnNames = [],
        Rows = [],
        ErrorMessage = errorMessage
    };
}
