namespace AIKernel.Cuda.Libtorch.Cuda13.Interop;

using System.Runtime.InteropServices;

internal static class NativeMethods
{
    internal const string LibraryName = "libtorch_bridge";

    [DllImport(LibraryName, EntryPoint = "load_model")]
    internal static extern int LoadModel(
        [MarshalAs(UnmanagedType.LPUTF8Str)]
        string path);

    [DllImport(LibraryName, EntryPoint = "unload_model")]
    [return: MarshalAs(UnmanagedType.I4)]
    internal static extern int UnloadModel(
        int handle);

    [DllImport(LibraryName, EntryPoint = "forward")]
    [return: MarshalAs(UnmanagedType.I4)]
    internal static extern int Forward(
        int handle,
        int[] inputIds,
        int length,
        out ForwardResultNative result);
}
