namespace AIKernel.Kernel.Memory;

using System.Runtime.InteropServices;

internal static class PosixNative
{
    /// <summary>
    /// EN: Gets the OpenReadOnly constant.
    /// [EN] Documents this public package API member. [JA] OpenReadOnly 定数を取得します。
    /// </summary>
    public const int OpenReadOnly = 0;
    /// <summary>
    /// EN: Gets the OpenReadWrite constant.
    /// [EN] Documents this public package API member. [JA] OpenReadWrite 定数を取得します。
    /// </summary>
    public const int OpenReadWrite = 2;
    /// <summary>
    /// EN: Gets the ProtRead constant.
    /// [EN] Documents this public package API member. [JA] ProtRead 定数を取得します。
    /// </summary>
    public const int ProtRead = 0x1;
    /// <summary>
    /// EN: Gets the ProtWrite constant.
    /// [EN] Documents this public package API member. [JA] ProtWrite 定数を取得します。
    /// </summary>
    public const int ProtWrite = 0x2;
    /// <summary>
    /// EN: Gets the MapShared constant.
    /// [EN] Documents this public package API member. [JA] MapShared 定数を取得します。
    /// </summary>
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
