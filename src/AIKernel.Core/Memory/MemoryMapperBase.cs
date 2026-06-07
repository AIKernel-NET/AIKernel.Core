using AIKernel.Common.Results;
using AIKernel.Abstractions.Memory;
using AIKernel.Enums;

namespace AIKernel.Core.Memory;

public abstract class MemoryMapperBase : IMemoryMapper
{
    public IMemoryRegion Open(
        string path,
        MemoryAccessMode accessMode = MemoryAccessMode.Read)
    {
        var result = OpenResult(path, accessMode);
        if (result.IsSuccess)
            return result.Value!;

        throw new InvalidOperationException(
            result.Error?.Message ?? "Memory mapping failed.");
    }

    public Result<IMemoryRegion> OpenResult(
        string path,
        MemoryAccessMode accessMode = MemoryAccessMode.Read)
    {
        if (!Enum.IsDefined(accessMode))
            return Result<IMemoryRegion>.Fail(MemoryMappingErrors.Error(
                "Memory access mode is invalid."));

        var normalized = NormalizePath(path);
        if (normalized.IsFailure)
            return Result<IMemoryRegion>.Fail(normalized.Error!);

        try
        {
            return OpenCore(normalized.Value!, accessMode);
        }
        catch (Exception ex)
        {
            return Result<IMemoryRegion>.Fail(MemoryMappingErrors.FromException(ex));
        }
    }

    protected abstract Result<IMemoryRegion> OpenCore(
        string path,
        MemoryAccessMode accessMode);

    private static Result<string> NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return Result<string>.Fail(MemoryMappingErrors.Error("Memory mapped path is required."));

        try
        {
            return Result<string>.Success(Path.GetFullPath(path));
        }
        catch (Exception ex)
        {
            return Result<string>.Fail(MemoryMappingErrors.FromException(ex));
        }
    }
}
