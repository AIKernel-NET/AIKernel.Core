namespace AIKernel.Core.Tests.Rom;

using AIKernel.Abstractions.Rom;
using AIKernel.Core.Rom;
using AIKernel.Dtos.Rom;
using AIKernel.Vfs;
using Xunit;

public abstract class RomLoaderContractTests
{
    protected abstract IRomLoader CreateLoader();

    protected abstract IVfsSession CreateReadableSession();

    protected abstract string ValidRomPath { get; }

    protected abstract string MissingRomIdPath { get; }

    protected abstract string InvalidSignaturePath { get; }

    [Fact]
    public async Task LoadAsync_ReturnsImmutableRomSnapshot_WhenMarkdownIsValid()
    {
        // Arrange
        var loader = CreateLoader();
        await using var session = CreateReadableSession();

        // Act
        var snapshot = await loader.LoadAsync(session, ValidRomPath);

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(snapshot.RomId.Value));
        Assert.NotEmpty(snapshot.Body);
        Assert.NotNull(snapshot.Signature);
        Assert.True(snapshot.Signature.IsVerified);

        Assert.Throws<NotSupportedException>(
            () => ((ICollection<RomRelationSnapshot>)snapshot.Relations).Add(
                new RomRelationSnapshot("x", "related")));
    }

    [Fact]
    public async Task LoadAsync_FailsClosed_WhenRomIdIsMissing()
    {
        // Arrange
        var loader = CreateLoader();
        await using var session = CreateReadableSession();

        // Act / Assert
        await Assert.ThrowsAsync<RomLoadException>(
            () => loader.LoadAsync(session, MissingRomIdPath));
    }

    [Fact]
    public async Task LoadAsync_FailsClosed_WhenSignatureDoesNotMatch()
    {
        // Arrange
        var loader = CreateLoader();
        await using var session = CreateReadableSession();

        // Act / Assert
        await Assert.ThrowsAsync<RomSignatureVerificationException>(
            () => loader.LoadAsync(session, InvalidSignaturePath));
    }

    [Fact]
    public async Task LoadAsync_PerformsVfsReadOnlyBeforeSnapshotCreation()
    {
        // Arrange
        var loader = CreateLoader();
        await using var session = CreateReadableSession();

        // Act
        var snapshot = await loader.LoadAsync(session, ValidRomPath);

        // Assert
        Assert.Equal(ValidRomPath, snapshot.SourcePath);
        Assert.True(snapshot.LoadedAtUtc != default);
    }
}
