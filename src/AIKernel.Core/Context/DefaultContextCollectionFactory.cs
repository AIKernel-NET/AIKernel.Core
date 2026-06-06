namespace AIKernel.Core.Context;

using AIKernel.Abstractions.Context;
using AIKernel.Dtos.Context;
using AIKernel.Dtos.Rom;
using AIKernel.Enums;

public sealed class DefaultContextCollectionFactory : IContextCollectionFactory
{
    public IContextCollection Create(
        IReadOnlyList<RomSnapshot> roms,
        IReadOnlyList<RomContextEdge> edges,
        ContextAssemblyScope scope)
    {
        ArgumentNullException.ThrowIfNull(roms);
        ArgumentNullException.ThrowIfNull(edges);
        ArgumentNullException.ThrowIfNull(scope);

        var fragments = roms
            .OrderBy(x => x.RomId.Value, StringComparer.Ordinal)
            .Select(rom => new ContextFragment
            {
                FragmentId = rom.RomId.Value,
                Category = ContextCategory.Material,
                Content = rom.Body,
                Priority = 0.8,
                CreatedAt = rom.LoadedAtUtc.UtcDateTime,
                Metadata = BuildMetadata(rom, edges)
            })
            .ToArray();

        return new ContextCollectionSnapshot(fragments);
    }

    private static IReadOnlyDictionary<string, string> BuildMetadata(
        RomSnapshot rom,
        IReadOnlyList<RomContextEdge> edges)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["rom_id"] = rom.RomId.Value,
            ["source_path"] = rom.SourcePath,
            ["signature_algorithm"] = rom.Signature.Algorithm,
            ["signature_hash"] = rom.Signature.ActualHash,
            ["security_tags"] = string.Join(",", rom.SecurityTags.Order(StringComparer.Ordinal))
        };

        foreach (var item in rom.AdditionalMetadata.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            metadata["rom_metadata:" + item.Key] = item.Value;
        }

        var outgoing = edges
            .Where(x => x.SourceRomId == rom.RomId)
            .OrderBy(x => x.TargetRomId.Value, StringComparer.Ordinal)
            .ThenBy(x => x.Kind, StringComparer.Ordinal)
            .Select(x => $"{x.Kind}:{x.TargetRomId.Value}");

        metadata["relations"] = string.Join(",", outgoing);

        return metadata;
    }
}
