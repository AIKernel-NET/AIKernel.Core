namespace AIKernel.Core.Context;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIKernel.Abstractions.Context;
using AIKernel.Dtos.Context;
using AIKernel.Dtos.Rom;

public sealed class DefaultContextHashCalculator : IContextHashCalculator
{
    public string ComputeHash(
        ContextAssemblyRequest request,
        IReadOnlyList<RomSnapshot> roms,
        IReadOnlyList<RomContextEdge> edges)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(roms);
        ArgumentNullException.ThrowIfNull(edges);

        var payload = new
        {
            root_rom_id = request.RootRomId.Value,
            scope = new
            {
                purpose = request.Scope.Purpose,
                capabilities = request.Scope.Capabilities
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToArray(),
                metadata = request.Scope.Metadata
                    .OrderBy(x => x.Key, StringComparer.Ordinal)
                    .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal)
            },
            roms = roms
                .OrderBy(x => x.RomId.Value, StringComparer.Ordinal)
                .Select(x => new
                {
                    rom_id = x.RomId.Value,
                    source_path = x.SourcePath,
                    signature_algorithm = x.Signature.Algorithm,
                    signature_hash = x.Signature.ActualHash,
                    security_tags = x.SecurityTags
                        .OrderBy(tag => tag, StringComparer.Ordinal)
                        .ToArray(),
                    body_hash = ComputeBodyHash(x.Body)
                })
                .ToArray(),
            edges = edges
                .OrderBy(x => x.SourceRomId.Value, StringComparer.Ordinal)
                .ThenBy(x => x.TargetRomId.Value, StringComparer.Ordinal)
                .ThenBy(x => x.Kind, StringComparer.Ordinal)
                .Select(x => new
                {
                    source = x.SourceRomId.Value,
                    target = x.TargetRomId.Value,
                    kind = x.Kind
                })
                .ToArray()
        };

        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);
        var hash = SHA256.HashData(bytes);

        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string ComputeBodyHash(string body)
    {
        var normalized = body
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal);

        var bytes = Encoding.UTF8.GetBytes(normalized);
        var hash = SHA256.HashData(bytes);

        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
