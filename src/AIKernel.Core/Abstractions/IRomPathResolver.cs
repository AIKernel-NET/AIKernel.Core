#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Abstractions.Context;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

using AIKernel.Dtos.Rom;

public interface IRomPathResolver
{
    ValueTask<string> ResolvePathAsync(
        RomId romId,
        CancellationToken cancellationToken = default);
}
