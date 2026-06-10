namespace AIKernel.Core.Vfs.Abstractions;

using AIKernel.Common.Results;
using AIKernel.Dtos.Vfs;
using AIKernel.Vfs;
using System;
using System.Collections.Generic;

internal static class VfsEntryQueryEngine
{
    private static readonly string[] Columns =
    [
        "Name",
        "Path",
        "Type",
        "Size",
        "CreatedAtUtc",
        "ModifiedAtUtc"
    ];

    public static IVfsQueryResult Execute(IEnumerable<VfsEntry> source, IVfsQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (source is null)
        {
            return VfsQueryResultSnapshot.Failure("VFS query source is required.");
        }

        if (!string.Equals(query.QueryType, "entries", StringComparison.OrdinalIgnoreCase))
        {
            return VfsQueryResultSnapshot.Failure(
                $"Unsupported VFS query type: {query.QueryType}");
        }

        if (query.Offset is < 0)
        {
            return VfsQueryResultSnapshot.Failure("VFS query offset must be greater than or equal to zero.");
        }

        if (query.Limit is < 0)
        {
            return VfsQueryResultSnapshot.Failure("VFS query limit must be greater than or equal to zero.");
        }

        var entries = source;

        if (query.Filters is not null)
        {
            if (query.Filters.TryGetValue("pathPrefix", out var prefix))
            {
                var normalizedPrefix = Try.Run(() => VfsPathRules.Normalize(prefix))
                    .Match(
                        error => Either<string, string>.FromLeft(error.Message),
                        Either<string, string>.FromRight);
                var filterError = normalizedPrefix.Match<string?>(
                    error => error,
                    valid =>
                    {
                        entries = entries.Where(x => VfsPathRules.IsUnder(valid, x.Path));
                        return null;
                    });
                if (filterError is not null)
                {
                    return VfsQueryResultSnapshot.Failure(filterError);
                }
            }

            if (query.Filters.TryGetValue("type", out var type))
            {
                entries = entries.Where(x =>
                    string.Equals(x.Type.ToString(), type, StringComparison.OrdinalIgnoreCase));
            }
        }

        entries = ApplySort(entries, query.Sort);

        if (query.Offset is > 0)
        {
            entries = entries.Skip(query.Offset.Value);
        }

        if (query.Limit is > -1)
        {
            entries = entries.Take(query.Limit.Value);
        }

        var rows = entries
            .Select(x => new VfsQueryRow
            {
                Data = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["Name"] = x.Name,
                    ["Path"] = x.Path,
                    ["Type"] = x.Type.ToString(),
                    ["Size"] = x.Size.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    ["CreatedAtUtc"] = x.CreatedAt.ToUniversalTime().ToString("O"),
                    ["ModifiedAtUtc"] = x.ModifiedAt.ToUniversalTime().ToString("O")
                }
            })
            .ToArray();

        return new VfsQueryResultSnapshot
        {
            IsSuccessful = true,
            RowCount = rows.Length,
            ColumnNames = Columns,
            Rows = rows
        };
    }

    private static IEnumerable<VfsEntry> ApplySort(
        IEnumerable<VfsEntry> entries,
        IReadOnlyList<VfsQuerySort>? sort)
    {
        if (sort is null || sort.Count == 0)
        {
            return entries.OrderBy(x => x.Path, StringComparer.Ordinal);
        }

        IOrderedEnumerable<VfsEntry>? ordered = null;

        foreach (var item in sort)
        {
            Func<VfsEntry, object> key = item.FieldName switch
            {
                "Name" => x => x.Name,
                "Path" => x => x.Path,
                "Type" => x => x.Type,
                "Size" => x => x.Size,
                "CreatedAtUtc" => x => x.CreatedAt,
                "ModifiedAtUtc" => x => x.ModifiedAt,
                _ => x => x.Path
            };

            ordered = ordered is null
                ? item.Ascending
                    ? entries.OrderBy(key)
                    : entries.OrderByDescending(key)
                : item.Ascending
                    ? ordered.ThenBy(key)
                    : ordered.ThenByDescending(key);
        }

        return ordered ?? entries.OrderBy(x => x.Path, StringComparer.Ordinal);
    }
}
