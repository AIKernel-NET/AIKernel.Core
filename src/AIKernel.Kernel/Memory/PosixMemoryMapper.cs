namespace AIKernel.Kernel.Memory;

using System.Runtime.InteropServices;
using AIKernel.Common.Results;
using AIKernel.Core.Memory;
using Microsoft.Win32.SafeHandles;

/// <summary>[EN] Documents this public package API member. [JA] PosixMemoryMapper を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Kernel.Memory.PosixMemoryMapper']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Kernel.Memory.PosixMemoryMapper']/summary" />
public sealed class PosixMemoryMapper : MemoryMapperBase, ISharedMemoryMapper
{
    /// <inheritdoc />
    public Result<IMemoryRegion> OpenShared(string channelName, long capacity)
    {
        if (!OperatingSystem.IsLinux() || !SharedMemoryChannel.IsValid(channelName, capacity))
            return SharedMemoryChannel.Fail("Expected a Linux shared memory name and positive capacity.");

        int descriptor = -1;
        nint pointer = -1;
        try
        {
            descriptor = PosixNative.OpenShared("/" + channelName, PosixNative.OpenReadWrite, 0);
            if (descriptor < 0)
                return SharedMemoryChannel.Fail($"shm_open returned errno {Marshal.GetLastPInvokeError()}.");

            using var borrowed = new SafeFileHandle(descriptor, ownsHandle: false);
            if (RandomAccess.GetLength(borrowed) < capacity)
                return SharedMemoryChannel.Fail("The shared object is shorter than requested.");

            pointer = PosixNative.Mmap(0, (nuint)capacity, PosixNative.ProtRead | PosixNative.ProtWrite,
                PosixNative.MapShared, descriptor, 0);
            if (pointer == -1)
                return SharedMemoryChannel.Fail($"mmap returned errno {Marshal.GetLastPInvokeError()}.");

            var region = new PosixMemoryRegion(new MemoryRegionInfo(channelName, capacity, MemoryAccessMode.ReadWrite),
                pointer, descriptor);
            descriptor = -1;
            pointer = -1;
            return Result<IMemoryRegion>.Success(region);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException
            or DllNotFoundException or EntryPointNotFoundException or OutOfMemoryException)
        {
            return SharedMemoryChannel.Fail(ex.Message);
        }
        finally
        {
            if (pointer != -1) _ = PosixNative.Munmap(pointer, (nuint)capacity);
            if (descriptor >= 0) _ = PosixNative.Close(descriptor);
        }
    }

    /// <summary>EN: Executes the OpenCore operation on the AIKernel public contract surface. JA: AIKernel の公開契約サーフェスで OpenCore 操作を実行します。</summary>
    protected override Result<IMemoryRegion> OpenCore(
        string path,
        MemoryAccessMode accessMode)
    {
        if (OperatingSystem.IsWindows())
        {
            return Result<IMemoryRegion>.Fail(KernelMemoryMappingErrors.Error(
                "POSIX memory mapping is not available on Windows."));
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

        var flags = accessMode == MemoryAccessMode.Read
            ? PosixNative.OpenReadOnly
            : PosixNative.OpenReadWrite;
        var descriptor = PosixNative.Open(path, flags, 0);
        if (descriptor < 0)
        {
            return Result<IMemoryRegion>.Fail(KernelMemoryMappingErrors.Error(
                $"open failed with errno {Marshal.GetLastPInvokeError()}."));
        }

        var protection = accessMode == MemoryAccessMode.Read
            ? PosixNative.ProtRead
            : PosixNative.ProtRead | PosixNative.ProtWrite;
        var pointer = PosixNative.Mmap(
            IntPtr.Zero,
            (UIntPtr)fileInfo.Length,
            protection,
            PosixNative.MapShared,
            descriptor,
            IntPtr.Zero);

        if (pointer == new IntPtr(-1))
        {
            _ = PosixNative.Close(descriptor);
            return Result<IMemoryRegion>.Fail(KernelMemoryMappingErrors.Error(
                $"mmap failed with errno {Marshal.GetLastPInvokeError()}."));
        }

        return Result<IMemoryRegion>.Success(new PosixMemoryRegion(
            new MemoryRegionInfo(path, fileInfo.Length, accessMode),
            pointer,
            descriptor));
    }
}
