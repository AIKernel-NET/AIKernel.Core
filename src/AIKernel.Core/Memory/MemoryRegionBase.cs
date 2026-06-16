using AIKernel.Common.Results;

namespace AIKernel.Core.Memory;

/// <summary>EN: Documentation for public API. JA: MemoryRegionBase を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Memory.MemoryRegionBase']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Memory.MemoryRegionBase']/summary" />
public abstract class MemoryRegionBase : IMemoryRegion
{
    private bool _disposed;

    /// <summary>EN: Initializes a new instance for the MemoryRegionBase AIKernel contract surface. JA: MemoryRegionBase AIKernel 契約サーフェスの新しいインスタンスを初期化します。</summary>
    protected MemoryRegionBase(
        MemoryRegionInfo info,
        IntPtr pointer)
    {
        Info = info ?? throw new ArgumentNullException(nameof(info));
        if (Info.Length < 0)
            throw new ArgumentOutOfRangeException(
                nameof(info),
                "Memory region length must be greater than or equal to zero.");

        Pointer = pointer;
    }

    /// <summary>EN: Documentation for public API. JA: Info を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Memory.MemoryRegionBase.Info']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Memory.MemoryRegionBase.Info']/summary" />
    public MemoryRegionInfo Info { get; }

    /// <summary>EN: Documentation for public API. JA: Pointer を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Memory.MemoryRegionBase.Pointer']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Memory.MemoryRegionBase.Pointer']/summary" />
    public IntPtr Pointer { get; protected set; }

    /// <summary>EN: Documentation for public API. JA: Length を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Core.Memory.MemoryRegionBase.Length']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Core.Memory.MemoryRegionBase.Length']/summary" />
    public long Length => Info.Length;

    /// <summary>EN: Documentation for public API. JA: IsMapped を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Core.Memory.MemoryRegionBase.IsMapped']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Core.Memory.MemoryRegionBase.IsMapped']/summary" />
    public bool IsMapped => !_disposed && Pointer != IntPtr.Zero;

    /// <summary>EN: Documentation for public API. JA: Unmap を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Memory.MemoryRegionBase.Unmap']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Memory.MemoryRegionBase.Unmap']/summary" />
    public bool Unmap()
        => UnmapResult().Match(_ => false, value => value);

    /// <summary>EN: Documentation for public API. JA: UnmapResult を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Memory.MemoryRegionBase.UnmapResult']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Memory.MemoryRegionBase.UnmapResult']/summary" />
    public Result<bool> UnmapResult()
    {
        if (_disposed)
            return Result<bool>.Success(true);

        return Try
            .Run(UnmapCore)
            .Match(
                error => Result<bool>.Fail(MemoryMappingErrors.FromContext(error)),
                result => result.Tap(_ =>
                {
                    Pointer = IntPtr.Zero;
                    _disposed = true;
                }));
    }

    /// <summary>EN: Documentation for public API. JA: Dispose を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Memory.MemoryRegionBase.Dispose']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Memory.MemoryRegionBase.Dispose']/summary" />
    public void Dispose()
    {
        _ = UnmapResult();
        GC.SuppressFinalize(this);
    }

    /// <summary>EN: Executes the UnmapCore operation on the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで UnmapCore 操作を実行します。</summary>
    protected abstract Result<bool> UnmapCore();
}
