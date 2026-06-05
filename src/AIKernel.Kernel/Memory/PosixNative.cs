namespace AIKernel.Kernel.Memory;

using System.Runtime.InteropServices;

internal static class PosixNative
{
    public const int OpenReadOnly = 0;
    public const int OpenReadWrite = 2;
    public const int ProtRead = 0x1;
    public const int ProtWrite = 0x2;
    public const int MapShared = 0x01;

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    internal static extern int Open(
        [MarshalAs(UnmanagedType.LPUTF8Str)]
        string path,
        int flags,
        int mode);

    [DllImport("libc", EntryPoint = "mmap", SetLastError = true)]
    internal static extern IntPtr Mmap(
        IntPtr address,
        UIntPtr length,
        int protection,
        int flags,
        int fileDescriptor,
        IntPtr offset);

    [DllImport("libc", EntryPoint = "munmap", SetLastError = true)]
    internal static extern int Munmap(
        IntPtr address,
        UIntPtr length);

    [DllImport("libc", EntryPoint = "close", SetLastError = true)]
    internal static extern int Close(
        int fileDescriptor);
}
