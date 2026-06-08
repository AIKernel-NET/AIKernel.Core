namespace AIKernel.Core.Context;

using AIKernel.Abstractions.Context;
using AIKernel.Dtos.Rom;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Context.DictionaryRomPathResolver']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Context.DictionaryRomPathResolver']" />
public sealed class DictionaryRomPathResolver : IRomPathResolver
{
    private readonly IReadOnlyDictionary<RomId, string> _paths;

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.DictionaryRomPathResolver.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.DictionaryRomPathResolver.#ctor']" />
    public DictionaryRomPathResolver(IReadOnlyDictionary<RomId, string> paths)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.DictionaryRomPathResolver.ResolvePathAsync']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.DictionaryRomPathResolver.ResolvePathAsync']" />
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
