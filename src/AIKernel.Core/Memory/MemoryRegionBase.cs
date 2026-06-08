using AIKernel.Common.Results;

namespace AIKernel.Core.Memory;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Memory.MemoryRegionBase']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Memory.MemoryRegionBase']" />
public abstract class MemoryRegionBase : IMemoryRegion
{
    private bool _disposed;

    /// <summary>Initializes a new instance for the MemoryRegionBase AIKernel contract surface. JA: MemoryRegionBase AIKernel 契約サーフェスの新しいインスタンスを初期化します。</summary>
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

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Memory.MemoryRegionBase.Info']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Memory.MemoryRegionBase.Info']" />
    public MemoryRegionInfo Info { get; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Memory.MemoryRegionBase.Pointer']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Memory.MemoryRegionBase.Pointer']" />
    public IntPtr Pointer { get; protected set; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Core.Memory.MemoryRegionBase.Length']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Core.Memory.MemoryRegionBase.Length']" />
    public long Length => Info.Length;

    /// <include file="docs.en.xml" path="doc/members/member[@name='F:AIKernel.Core.Memory.MemoryRegionBase.IsMapped']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='F:AIKernel.Core.Memory.MemoryRegionBase.IsMapped']" />
    public bool IsMapped => !_disposed && Pointer != IntPtr.Zero;

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Memory.MemoryRegionBase.Unmap']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Memory.MemoryRegionBase.Unmap']" />
    public bool Unmap()
        => UnmapResult().IsSuccess;

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Memory.MemoryRegionBase.UnmapResult']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Memory.MemoryRegionBase.UnmapResult']" />
    public Result<bool> UnmapResult()
    {
        if (_disposed)
            return Result<bool>.Success(true);

        try
        {
            var result = UnmapCore();
            if (result.IsSuccess)
            {
                Pointer = IntPtr.Zero;
                _disposed = true;
            }

            return result;
        }
        catch (Exception ex)
        {
            return Result<bool>.Fail(MemoryMappingErrors.FromException(ex));
        }
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Memory.MemoryRegionBase.Dispose']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Memory.MemoryRegionBase.Dispose']" />
    public void Dispose()
    {
        _ = UnmapResult();
        GC.SuppressFinalize(this);
    }

    /// <summary>Executes the UnmapCore operation on the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで UnmapCore 操作を実行します。</summary>
    protected abstract Result<bool> UnmapCore();
}
