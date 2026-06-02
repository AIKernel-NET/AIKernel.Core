namespace AIKernel.Core.Context;

using AIKernel.Abstractions.Context;
using AIKernel.Dtos.Rom;

public sealed class DictionaryRomPathResolver : IRomPathResolver
{
    private readonly IReadOnlyDictionary<RomId, string> _paths;

    public DictionaryRomPathResolver(IReadOnlyDictionary<RomId, string> paths)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    public ValueTask<string> ResolvePathAsync(
        RomId romId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_paths.TryGetValue(romId, out var path)
            || string.IsNullOrWhiteSpace(path))
        {
            throw new KeyNotFoundException(
                $"ROM path mapping was not found. RomId='{romId.Value}'.");
        }

        return ValueTask.FromResult(path);
    }
}
