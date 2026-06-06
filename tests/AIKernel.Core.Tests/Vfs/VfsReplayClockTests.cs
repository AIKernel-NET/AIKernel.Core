namespace AIKernel.Core.Tests.Vfs;

using AIKernel.Core.Time;
using AIKernel.Core.Vfs.Memory;
using AIKernel.Vfs;

public sealed class VfsReplayClockTests
{
    [Fact]
    public async Task MemoryFileProvider_UsesKernelClockNow_ForSnapshotTimestamps()
    {
        var fixedUtcNow = new DateTimeOffset(
            2026,
            5,
            12,
            0,
            0,
            0,
            TimeSpan.Zero);

        var clock = KernelClock.Replay(fixedUtcNow);

        var provider = new MemoryFileProvider(
            new MemoryFileProviderOptions
            {
                Clock = clock
            });

        await using var session = await provider.OpenSessionAsync(new TestVfsCredentials());

        await session.WriteFileAsync("rom/a.md", "hello"u8.ToArray());

        var file = await session.ReadFileAsync("rom/a.md");

        Assert.Equal(fixedUtcNow.UtcDateTime, file.CreatedAt);
        Assert.Equal(fixedUtcNow.UtcDateTime, file.ModifiedAt);
    }

    private sealed class TestVfsCredentials : IVfsCredentials
    {
        public string? Username => "test";

        public string? ApiKey => null;

        public string? Token => "test-token";

        public IReadOnlyDictionary<string, object>? Parameters => null;
    }
}
