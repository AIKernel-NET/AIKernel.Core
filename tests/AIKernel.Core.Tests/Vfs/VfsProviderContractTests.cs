namespace AIKernel.Core.Tests.Vfs;

using AIKernel.Vfs;
using Xunit;

public abstract class VfsProviderContractTests
{
    protected abstract IVfsProvider CreateProvider();

    protected abstract IVfsCredentials CreateCredentials();

    protected abstract string ExistingFilePath { get; }

    protected abstract byte[] ExistingFileContent { get; }

    protected virtual bool SupportsWrite => true;

    protected virtual bool SupportsDelete => true;

    protected virtual bool SupportsEntryQuery => true;

    [Fact]
    public async Task OpenSessionAsync_Throws_WhenCredentialsIsNull()
    {
        // Arrange
        var provider = CreateProvider();

        // Act / Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => provider.OpenSessionAsync(credentials: null!));
    }

    [Fact]
    public async Task OpenSessionAsync_ReturnsSession_WhenCredentialsAreAccepted()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        await using var session = await provider.OpenSessionAsync(CreateCredentials());

        // Assert
        Assert.NotNull(session);
        Assert.False(string.IsNullOrWhiteSpace(session.SessionId));
    }

    [Fact]
    public async Task ReadFileAsync_ReturnsStableContent_ForSamePath()
    {
        // Arrange
        var provider = CreateProvider();
        await using var session = await provider.OpenSessionAsync(CreateCredentials());

        // Act
        var first = await session.ReadFileAsync(ExistingFilePath);
        var second = await session.ReadFileAsync(ExistingFilePath);

        // Assert
        Assert.Equal(ExistingFileContent, await first.ReadAsync());
        Assert.Equal(await first.ReadAsync(), await second.ReadAsync());
    }

    [Fact]
    public async Task ReadAsync_ReturnsDefensiveCopy()
    {
        // Arrange
        var provider = CreateProvider();
        await using var session = await provider.OpenSessionAsync(CreateCredentials());

        // Act
        var file = await session.ReadFileAsync(ExistingFilePath);
        var first = await file.ReadAsync();
        var second = await file.ReadAsync();

        // Assert
        Assert.NotSame(first, second);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task WriteFileAsync_FollowsProviderCapability()
    {
        // Arrange
        var provider = CreateProvider();
        await using var session = await provider.OpenSessionAsync(CreateCredentials());

        // Act / Assert
        if (SupportsWrite)
        {
            await session.WriteFileAsync("contract/new.txt", "new"u8.ToArray());
            Assert.True(await session.ExistsAsync("contract/new.txt"));
        }
        else
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => session.WriteFileAsync("contract/new.txt", "new"u8.ToArray()));
        }
    }

    [Fact]
    public async Task DeleteAsync_FollowsProviderCapability()
    {
        // Arrange
        var provider = CreateProvider();
        await using var session = await provider.OpenSessionAsync(CreateCredentials());

        // Act / Assert
        if (SupportsDelete)
        {
            await session.WriteFileAsync("contract/delete-me.txt", "x"u8.ToArray());
            await session.DeleteAsync("contract/delete-me.txt");
            Assert.False(await session.ExistsAsync("contract/delete-me.txt"));
        }
        else
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => session.DeleteAsync(ExistingFilePath));
        }
    }

    [Fact]
    public async Task QueryAsync_ReturnsDeterministicRows_WhenSupported()
    {
        // Arrange
        var provider = CreateProvider();
        await using var session = await provider.OpenSessionAsync(CreateCredentials());

        var query = new VfsEntryQuery(
            QueryType: "entries",
            Filters: null,
            Limit: null,
            Offset: null,
            Sort: null);

        // Act
        var first = await session.QueryAsync(query);
        var second = await session.QueryAsync(query);

        // Assert
        if (SupportsEntryQuery)
        {
            Assert.True(first.IsSuccessful);
            Assert.True(second.IsSuccessful);
            Assert.Equal(
                first.Rows.Select(x => x.Data["Path"]),
                second.Rows.Select(x => x.Data["Path"]));
        }
        else
        {
            Assert.False(first.IsSuccessful);
        }
    }

    private sealed record VfsEntryQuery(
        string QueryType,
        IReadOnlyDictionary<string, string>? Filters,
        int? Limit,
        int? Offset,
        IReadOnlyList<AIKernel.Dtos.Vfs.VfsQuerySort>? Sort) : IVfsQuery;
}