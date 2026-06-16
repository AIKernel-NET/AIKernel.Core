namespace AIKernel.Kernel;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIKernel.Abstractions.Kernel;
using AIKernel.Dtos.Kernel;

/// <summary>EN: Documentation for public API. JA: KernelRequestHasher を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Kernel.KernelRequestHasher']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Kernel.KernelRequestHasher']/summary" />
public sealed class KernelRequestHasher : IKernelRequestHasher
{
    /// <summary>EN: Documentation for public API. JA: ComputeHash を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Kernel.KernelRequestHasher.ComputeHash']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Kernel.KernelRequestHasher.ComputeHash']/summary" />
    public string ComputeHash(KernelRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var payload = new
        {
            input = request.Input,
            root_rom_id = request.RootRomId.Value,
            vfs_provider_id = request.VfsProviderId,
            parent_snapshot_id = request.ParentSnapshotId,
            requested_model_id = request.RequestedModelId,
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
            metadata = request.Metadata
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal)
        };

        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);
        var hash = SHA256.HashData(bytes);

        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}