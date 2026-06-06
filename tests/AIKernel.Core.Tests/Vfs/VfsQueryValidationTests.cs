namespace AIKernel.Core.Tests.Vfs;

using AIKernel.Core.Time;
using AIKernel.Core.Vfs.Memory;
using AIKernel.Dtos.Vfs;
using AIKernel.Vfs;
using Xunit;

public sealed class VfsQueryValidationTests
{
    [Fact]
    public async Task QueryAsync_ReturnsFailure_ForNegativeOffset()
    {
        await using var session = await OpenSessionAsync();

        var result = await session.QueryAsync(new VfsEntryQuery(
            "entries",
            Filters: null,
            Limit: null,
            Offset: -1,
            Sort: null));

        Assert.False(result.IsSuccessful);
        Assert.Equal(0, result.RowCount);
        Assert.Contains("offset", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryAsync_ReturnsFailure_ForNegativeLimit()
    {
        await using var session = await OpenSessionAsync();

        var result = await session.QueryAsync(new VfsEntryQuery(
            "entries",
            Filters: null,
            Limit: -1,
            Offset: null,
            Sort: null));

        Assert.False(result.IsSuccessful);
        Assert.Equal(0, result.RowCount);
        Assert.Contains("limit", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QueryAsync_ReturnsFailure_ForPathTraversalFilter()
    {
        await using var session = await OpenSessionAsync();

        var result = await session.QueryAsync(new VfsEntryQuery(
            "entries",
            Filters: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["pathPrefix"] = "../escape"
            },
            Limit: null,
            Offset: null,
            Sort: null));

        Assert.False(result.IsSuccessful);
        Assert.Equal(0, result.RowCount);
        Assert.Contains("Path traversal", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<IVfsSession> OpenSessionAsync()
    {
        var provider = new MemoryFileProvider(
            new MemoryFileProviderOptions
            {
                Clock = KernelClock.Replay(DateTimeOffset.UnixEpoch)
            });
        var session = await provider.OpenSessionAsync(new TestVfsCredentials());
        await session.WriteFileAsync("rom/a.md", "hello"u8.ToArray());
        return session;
    }

    private sealed record VfsEntryQuery(
        string QueryType,
        IReadOnlyDictionary<string, string>? Filters,
        int? Limit,
        int? Offset,
        IReadOnlyList<VfsQuerySort>? Sort) : IVfsQuery;

    private sealed class TestVfsCredentials : IVfsCredentials
    {
        public string? Username => "test";

        public string? ApiKey => null;

        public string? Token => "test-token";

        public IReadOnlyDictionary<string, object>? Parameters => null;
    }
}
