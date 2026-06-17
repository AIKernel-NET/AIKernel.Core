namespace AIKernel.Core.Context;

using AIKernel.Abstractions.Context;
using AIKernel.Dtos.Rom;

/// <summary>[EN] Documents this public package API member. [JA] DictionaryRomPathResolver を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Context.DictionaryRomPathResolver']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Context.DictionaryRomPathResolver']/summary" />
public sealed class DictionaryRomPathResolver : IRomPathResolver
{
    private readonly IReadOnlyDictionary<RomId, string> _paths;

    /// <summary>[EN] Documents this public package API member. [JA] DictionaryRomPathResolver を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.DictionaryRomPathResolver.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.DictionaryRomPathResolver.#ctor']/summary" />
    public DictionaryRomPathResolver(IReadOnlyDictionary<RomId, string> paths)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
    }

    /// <summary>[EN] Documents this public package API member. [JA] ResolvePathAsync を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.DictionaryRomPathResolver.ResolvePathAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Context.DictionaryRomPathResolver.ResolvePathAsync']/summary" />
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
