namespace AIKernel.Cuda.Libtorch.Cuda13.Interop;

using System.Runtime.InteropServices;

internal static class NativeStatus
{
    internal const int Success = 0;
}

[StructLayout(LayoutKind.Sequential)]
internal struct ForwardResultNative
{
    public int Status;

    public int OutputTokenCount;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
    public int[] OutputTokenIds;

    public int LogitCount;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4096)]
    public float[] Logits;
}
