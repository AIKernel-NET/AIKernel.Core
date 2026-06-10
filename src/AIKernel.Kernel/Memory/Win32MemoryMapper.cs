namespace AIKernel.Kernel.Memory;

using AIKernel.Common.Results;
using AIKernel.Core.Memory;
using Microsoft.Win32.SafeHandles;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Kernel.Memory.Win32MemoryMapper']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Kernel.Memory.Win32MemoryMapper']/summary" />
public sealed class Win32MemoryMapper : MemoryMapperBase
{
    /// <summary>Executes the OpenCore operation on the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで OpenCore 操作を実行します。</summary>
    protected override Result<IMemoryRegion> OpenCore(
        string path,
        MemoryAccessMode accessMode)
    {
        if (!OperatingSystem.IsWindows())
        {
            return Result<IMemoryRegion>.Fail(KernelMemoryMappingErrors.Error(
                "Win32 memory mapping is only available on Windows."));
        }

        var fileInfo = new FileInfo(path);
        if (!fileInfo.Exists)
        {
            return Result<IMemoryRegion>.Fail(KernelMemoryMappingErrors.Error(
                $"Memory mapped file was not found: {path}."));
        }

        if (fileInfo.Length <= 0)
        {
            return Result<IMemoryRegion>.Fail(KernelMemoryMappingErrors.Error(
                "Memory mapped file must not be empty."));
        }

        var fileAccess = accessMode == MemoryAccessMode.Read
            ? FileAccess.Read
            : FileAccess.ReadWrite;
        var fileShare = accessMode == MemoryAccessMode.Read
            ? FileShare.Read
            : FileShare.ReadWrite;

        using var stream = new FileStream(
            path,
            FileMode.Open,
            fileAccess,
            fileShare,
            bufferSize: 1,
            FileOptions.RandomAccess);

        var protect = accessMode == MemoryAccessMode.Read
            ? Win32Native.PageReadonly
            : Win32Native.PageReadWrite;
        var desiredAccess = accessMode == MemoryAccessMode.Read
            ? Win32Native.FileMapRead
            : Win32Native.FileMapRead | Win32Native.FileMapWrite;

        var mappingHandle = Win32Native.CreateFileMapping(
            stream.SafeFileHandle,
            IntPtr.Zero,
            protect,
            0,
            0,
            null);

        if (mappingHandle.IsInvalid)
        {
            mappingHandle.Dispose();
            return Result<IMemoryRegion>.Fail(KernelMemoryMappingErrors.Error(
                "CreateFileMapping failed."));
        }

        var view = Win32Native.MapViewOfFile(
            mappingHandle,
            desiredAccess,
            0,
            0,
            UIntPtr.Zero);

        if (view == IntPtr.Zero)
        {
            mappingHandle.Dispose();
            return Result<IMemoryRegion>.Fail(KernelMemoryMappingErrors.Error(
                "MapViewOfFile failed."));
        }

        return Result<IMemoryRegion>.Success(new Win32MemoryRegion(
            new MemoryRegionInfo(path, fileInfo.Length, accessMode),
            view,
            mappingHandle));
    }
}
