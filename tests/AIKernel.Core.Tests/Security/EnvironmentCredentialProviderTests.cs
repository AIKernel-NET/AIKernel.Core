using System;
using System.Collections.Generic;
using System.Text;

namespace AIKernel.Core.Tests.Security;

using AIKernel.Abstractions.Security;
using AIKernel.Core.Security;
using Xunit;

public sealed class EnvironmentCredentialProviderTests : IDisposable
{
    private const string Key = "AIKERNEL_TEST_OPENAI_API_KEY";

    private readonly string? _originalValue;

    public EnvironmentCredentialProviderTests()
    {
        _originalValue = Environment.GetEnvironmentVariable(Key);
    }

    [Fact]
    public async Task GetSecretAsync_ReturnsSecret_WhenEnvironmentVariableExists()
    {
        Environment.SetEnvironmentVariable(Key, "sk-test-123456");

        var provider = new EnvironmentCredentialProvider();

        var secret = await provider.GetSecretAsync(Key, TestContext.Current.CancellationToken);

        Assert.Equal("sk-test-123456", secret);
    }

    [Fact]
    public async Task GetSecretAsync_FailsClosed_WhenEnvironmentVariableIsMissing()
    {
        Environment.SetEnvironmentVariable(Key, null);

        var provider = new EnvironmentCredentialProvider();

        await Assert.ThrowsAsync<SecureCredentialNotFoundException>(
            async () => await provider.GetSecretAsync(Key, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetSecretAsync_FailsClosed_WhenSecretHasLeadingOrTrailingWhitespace()
    {
        Environment.SetEnvironmentVariable(Key, " sk-test-123456 ");

        var provider = new EnvironmentCredentialProvider();

        await Assert.ThrowsAsync<SecureCredentialInvalidException>(
            async () => await provider.GetSecretAsync(Key, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetSecretAsync_FailsClosed_WhenSecretContainsControlCharacter()
    {
        Environment.SetEnvironmentVariable(Key, "sk-test-\n123456");

        var provider = new EnvironmentCredentialProvider();

        await Assert.ThrowsAsync<SecureCredentialInvalidException>(
            async () => await provider.GetSecretAsync(Key, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetSecretAsync_FailsClosed_WhenSecretIsTooShort()
    {
        Environment.SetEnvironmentVariable(Key, "short");

        var provider = new EnvironmentCredentialProvider();

        await Assert.ThrowsAsync<SecureCredentialInvalidException>(
            async () => await provider.GetSecretAsync(Key, TestContext.Current.CancellationToken));
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(Key, _originalValue);
    }
}
