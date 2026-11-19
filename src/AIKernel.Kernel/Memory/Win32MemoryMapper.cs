namespace AIKernel.Kernel.Memory;

using System.Runtime.InteropServices;
using AIKernel.Common.Results;
using AIKernel.Core.Memory;
using Microsoft.Win32.SafeHandles;

/// <summary>[EN] Documents this public package API member. [JA] Win32MemoryMapper を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Kernel.Memory.Win32MemoryMapper']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Kernel.Memory.Win32MemoryMapper']/summary" />
public sealed class Win32MemoryMapper : MemoryMapperBase, ISharedMemoryMapper
{
    /// <inheritdoc />
    public Result<IMemoryRegion> OpenShared(string channelName, long capacity)
    {
        if (!OperatingSystem.IsWindows() || !SharedMemoryChannel.IsValid(channelName, capacity))
            return SharedMemoryChannel.Fail("Expected a Windows shared memory name and positive capacity.");

        const uint access = Win32Native.FileMapRead | Win32Native.FileMapWrite;
        SafeFileHandle? handle = null;
        nint pointer = 0;
        try
        {
            handle = Win32Native.OpenFileMapping(access, false, channelName);
            if (handle.IsInvalid)
                return SharedMemoryChannel.Fail($"OpenFileMapping returned error {Marshal.GetLastPInvokeError()}.");
            pointer = Win32Native.MapViewOfFile(handle, access, 0, 0, (nuint)capacity);
            if (pointer == 0)
                return SharedMemoryChannel.Fail($"MapViewOfFile returned error {Marshal.GetLastPInvokeError()}.");

            var region = new Win32MemoryRegion(new MemoryRegionInfo(channelName, capacity, MemoryAccessMode.ReadWrite),
                pointer, handle);
            handle = null;
            pointer = 0;
            return Result<IMemoryRegion>.Success(region);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException
            or DllNotFoundException or EntryPointNotFoundException or OutOfMemoryException)
        {
            return SharedMemoryChannel.Fail(ex.Message);
        }
        finally
        {
            if (pointer != 0) _ = Win32Native.UnmapViewOfFile(pointer);
            handle?.Dispose();
        }
    }

    /// <summary>EN: Executes the OpenCore operation on the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで OpenCore 操作を実行します。</summary>
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
