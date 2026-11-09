namespace AIKernel.Core.Tests.Vfs;

using AIKernel.Core.Vfs.Abstractions;
using AIKernel.Vfs;
using Xunit;

public sealed class FileProviderBaseTests
{
    [Fact]
    public async Task DefaultAvailability_FailsClosed_WhenProviderDoesNotOverrideHealth()
    {
        var provider = new BareFileProvider();

        var available = await provider.IsAvailableAsync();
        var health = await provider.GetHealthAsync();

        Assert.False(available);
        Assert.False(health.IsHealthy);
        Assert.Equal(
            "VFS provider is using fail-closed base health; override GetHealthAsync for backend-specific probes.",
            health.Message);
    }

    private sealed class BareFileProvider : FileProviderBase
    {
        public BareFileProvider()
            : base(
                providerId: "bare",
                name: "Bare",
                clock: null,
                credentialValidator: null)
        {
        }

        protected override Task<IVfsSession> OpenSessionCoreAsync(string sessionId)
        {
            throw new InvalidOperationException("This test provider does not open sessions.");
        }
    }
}
