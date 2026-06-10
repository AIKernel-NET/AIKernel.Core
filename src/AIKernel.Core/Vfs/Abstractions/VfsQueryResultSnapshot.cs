using AIKernel.Dtos.Vfs;
using AIKernel.Vfs;
using System;
using System.Collections.Generic;
using System.Text;

namespace AIKernel.Core.Vfs.Abstractions;

internal sealed class VfsQueryResultSnapshot : IVfsQueryResult
{
    public required bool IsSuccessful { get; init; }

    public required int RowCount { get; init; }

    public required IReadOnlyList<string> ColumnNames { get; init; }

    public required IReadOnlyList<VfsQueryRow> Rows { get; init; }

    public string? ErrorMessage { get; init; }

    public static VfsQueryResultSnapshot Failure(string errorMessage) => new()
    {
        IsSuccessful = false,
        RowCount = 0,
        ColumnNames = [],
        Rows = [],
        ErrorMessage = errorMessage
    };
}
