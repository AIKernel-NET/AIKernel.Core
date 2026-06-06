using System;
using System.Collections.Generic;
using System.Text;

namespace AIKernel.Core.Tests.VFS;

using AIKernel.Core.Vfs.Abstractions;
using AIKernel.Vfs;
using Xunit;

public abstract class MemoryFileProviderContractTests
{
    protected abstract IVfsProvider CreateProvider();

    protected abstract IVfsCredentials CreateValidCredentials();

    protected abstract IVfsCredentials CreateInvalidCredentials();

    [Fact]
    public async Task OpenSessionAsync_ReturnsMemoryVfsSession_AsIVfsSession()
    {
        // Arrange
        var provider = CreateProvider();

        // Act
        await using var session = await provider.OpenSessionAsync(CreateValidCredentials());

        // Assert
        Assert.NotNull(session);
        Assert.IsAssignableFrom<IVfsSession>(session);
        Assert.False(string.IsNullOrWhiteSpace(session.SessionId));
    }

    [Fact]
    public async Task OpenSessionAsync_FailsClosed_WhenCredentialsAreInvalid()
    {
        // Arrange
        var provider = CreateProvider();

        // Act / Assert
        await Assert.ThrowsAsync<VfsAuthenticationFailedException>(
            () => provider.OpenSessionAsync(CreateInvalidCredentials()));
    }

    [Fact]
    public async Task ProviderIdentity_IsStable_AfterConstruction()
    {
        // Arrange
        var provider = CreateProvider();

        var providerId = provider.ProviderId;
        var name = provider.Name;

        // Act
        await using var first = await provider.OpenSessionAsync(CreateValidCredentials());
        await using var second = await provider.OpenSessionAsync(CreateValidCredentials());

        // Assert
        Assert.Equal(providerId, provider.ProviderId);
        Assert.Equal(name, provider.Name);
        Assert.NotEqual(first.SessionId, second.SessionId);
    }

    [Fact]
    public async Task ReadFileAsync_ReturnsDefensiveSnapshot()
    {
        // Arrange
        var provider = CreateProvider();
        await using var session = await provider.OpenSessionAsync(CreateValidCredentials());

        await session.WriteFileAsync("rom/a.md", "first"u8.ToArray());

        // Act
        var file = await session.ReadFileAsync("rom/a.md");
        var firstBytes = await file.ReadAsync();

        firstBytes[0] = (byte)'X';

        var secondBytes = await file.ReadAsync();

        // Assert
        Assert.Equal("first"u8.ToArray(), secondBytes);
    }

    [Fact]
    public async Task WriteFileAsync_DoesNotExposeMutableInputArray()
    {
        // Arrange
        var provider = CreateProvider();
        await using var session = await provider.OpenSessionAsync(CreateValidCredentials());

        var bytes = "canonical"u8.ToArray();

        // Act
        await session.WriteFileAsync("rom/a.md", bytes);
        bytes[0] = (byte)'X';

        var file = await session.ReadFileAsync("rom/a.md");
        var stored = await file.ReadAsync();

        // Assert
        Assert.Equal("canonical"u8.ToArray(), stored);
    }
}
