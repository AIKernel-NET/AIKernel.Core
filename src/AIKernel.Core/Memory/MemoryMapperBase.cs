using AIKernel.Common.Results;

namespace AIKernel.Core.Memory;

/// <summary>EN: Documentation for public API. JA: MemoryMapperBase を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Memory.MemoryMapperBase']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Memory.MemoryMapperBase']/summary" />
public abstract class MemoryMapperBase : IMemoryMapper
{
    /// <summary>EN: Documentation for public API. JA: Open を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Memory.MemoryMapperBase.Open']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Memory.MemoryMapperBase.Open']/summary" />
    public IMemoryRegion Open(
        string path,
        MemoryAccessMode accessMode = MemoryAccessMode.Read)
        => OpenResult(path, accessMode)
            .Match(
                error => throw new InvalidOperationException(error.Message),
                region => region);

    /// <summary>EN: Documentation for public API. JA: OpenResult を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Memory.MemoryMapperBase.OpenResult']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Memory.MemoryMapperBase.OpenResult']/summary" />
    public Result<IMemoryRegion> OpenResult(
        string path,
        MemoryAccessMode accessMode = MemoryAccessMode.Read)
    {
        if (!Enum.IsDefined(accessMode))
            return Result<IMemoryRegion>.Fail(MemoryMappingErrors.Error(
                "Memory access mode is invalid."));

        return
            from normalized in NormalizePath(path)
            from region in Try
                .Run(() => OpenCore(normalized, accessMode))
                .Match(
                    error => Result<IMemoryRegion>.Fail(MemoryMappingErrors.FromContext(error)),
                    result => result)
            select region;
    }

    /// <summary>EN: Executes the OpenCore operation on the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで OpenCore 操作を実行します。</summary>
    protected abstract Result<IMemoryRegion> OpenCore(
        string path,
        MemoryAccessMode accessMode);

    private static Result<string> NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return Result<string>.Fail(MemoryMappingErrors.Error("Memory mapped path is required."));

        return Try
            .Run(() => Path.GetFullPath(path))
            .Match(
                error => Result<string>.Fail(MemoryMappingErrors.FromContext(error)),
                Result<string>.Success);
    }
}
