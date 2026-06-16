namespace AIKernel.Kernel.Memory;

using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

internal static class Win32Native
{
    /// <summary>
    /// EN: Gets the PageReadonly constant.
    /// [EN] Documents this public package API member. [JA] PageReadonly 定数を取得します。
    /// </summary>
    public const uint PageReadonly = 0x02;
    /// <summary>
    /// EN: Gets the PageReadWrite constant.
    /// [EN] Documents this public package API member. [JA] PageReadWrite 定数を取得します。
    /// </summary>
    public const uint PageReadWrite = 0x04;
    /// <summary>
    /// EN: Gets the FileMapWrite constant.
    /// [EN] Documents this public package API member. [JA] FileMapWrite 定数を取得します。
    /// </summary>
    public const uint FileMapWrite = 0x0002;
    /// <summary>
    /// EN: Gets the FileMapRead constant.
    /// [EN] Documents this public package API member. [JA] FileMapRead 定数を取得します。
    /// </summary>
    public const uint FileMapRead = 0x0004;

    [DllImport(
        "kernel32.dll",
        EntryPoint = "CreateFileMappingW",
        SetLastError = true,
        CharSet = CharSet.Unicode)]
    internal static extern SafeFileHandle CreateFileMapping(
        SafeFileHandle fileHandle,
        IntPtr fileMappingAttributes,
        uint protect,
        uint maximumSizeHigh,
        uint maximumSizeLow,
        string? name);

    [DllImport(
        "kernel32.dll",
        EntryPoint = "MapViewOfFile",
        SetLastError = true)]
    internal static extern IntPtr MapViewOfFile(
        SafeFileHandle fileMappingObject,
        uint desiredAccess,
        uint fileOffsetHigh,
        uint fileOffsetLow,
        UIntPtr numberOfBytesToMap);

    [DllImport(
        "kernel32.dll",
        EntryPoint = "UnmapViewOfFile",
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool UnmapViewOfFile(
        IntPtr baseAddress);
}
