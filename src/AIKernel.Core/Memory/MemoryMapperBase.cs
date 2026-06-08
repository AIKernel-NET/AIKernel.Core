using AIKernel.Common.Results;

namespace AIKernel.Core.Memory;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Memory.MemoryMapperBase']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Memory.MemoryMapperBase']" />
public abstract class MemoryMapperBase : IMemoryMapper
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Memory.MemoryMapperBase.Open']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Memory.MemoryMapperBase.Open']" />
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

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Memory.MemoryMapperBase.OpenResult']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Memory.MemoryMapperBase.OpenResult']" />
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

    /// <summary>Executes the OpenCore operation on the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで OpenCore 操作を実行します。</summary>
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
