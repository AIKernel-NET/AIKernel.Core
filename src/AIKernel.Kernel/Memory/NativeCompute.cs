using System.Runtime.InteropServices;

namespace AIKernel.Kernel.Memory;

internal static unsafe partial class NativeCompute
{
    private const string Library = "aikernel_cuda_driver";
    internal const uint Version = 1;
    private const DllImportSearchPath Search = DllImportSearchPath.AssemblyDirectory | DllImportSearchPath.SafeDirectories;

    [DefaultDllImportSearchPaths(Search), LibraryImport(Library, EntryPoint = "ak_create")]
    internal static partial int Create(uint version, int ordinal, out nint context);
    [DefaultDllImportSearchPaths(Search), LibraryImport(Library, EntryPoint = "ak_destroy")]
    internal static partial int Destroy(nint context);
    [DefaultDllImportSearchPaths(Search), LibraryImport(Library, EntryPoint = "ak_device_alloc")]
    internal static partial int DeviceAllocate(nint context, nuint length, out ulong address);
    [DefaultDllImportSearchPaths(Search), LibraryImport(Library, EntryPoint = "ak_device_free")]
    internal static partial int DeviceFree(nint context, ulong address);
    [DefaultDllImportSearchPaths(Search), LibraryImport(Library, EntryPoint = "ak_pinned_alloc")]
    internal static partial int HostAllocate(nint context, nuint length, out nint address);
    [DefaultDllImportSearchPaths(Search), LibraryImport(Library, EntryPoint = "ak_pinned_free")]
    internal static partial int HostFree(nint context, nint address);
    [DefaultDllImportSearchPaths(Search), LibraryImport(Library, EntryPoint = "ak_upload")]
    internal static partial int Upload(nint context, ulong address, byte* source, nuint length);
    [DefaultDllImportSearchPaths(Search), LibraryImport(Library, EntryPoint = "ak_download")]
    internal static partial int Download(nint context, ulong address, byte* destination, nuint length);
    [DefaultDllImportSearchPaths(Search), LibraryImport(Library, EntryPoint = "ak_gemv")]
    internal static partial int Gemv(nint context, ulong address, float* input, float* output, uint rows, uint columns);
    [DefaultDllImportSearchPaths(Search), LibraryImport(Library, EntryPoint = "ak_normalize")]
    internal static partial int Normalize(nint context, float* input, float* scale, float* output,
        uint length, float epsilon, int center);
    [DefaultDllImportSearchPaths(Search), LibraryImport(Library, EntryPoint = "ak_rope")]
    internal static partial int Rope(nint context, float* input, float* sinCos, uint length);
    [DefaultDllImportSearchPaths(Search), LibraryImport(Library, EntryPoint = "ak_swiglu")]
    internal static partial int SwiGlu(nint context, float* gate, float* up, uint length);
    [DefaultDllImportSearchPaths(Search), LibraryImport(Library, EntryPoint = "ak_softmax")]
    internal static partial int Softmax(nint context, float* values, uint length);

    [DefaultDllImportSearchPaths(Search), LibraryImport(Library, EntryPoint = "ak_attention_tile")]
    internal static partial int AttentionTile(nint context, ulong address, float* input, float* output,
        uint rows, uint kvWidth, uint headOffset, uint headWidth, uint mode);

    internal static void Check(int status, string operation)
    {
        if (status == 4)
        {
            throw new OutOfMemoryException($"{operation} could not allocate native memory.");
        }

        if (status != 0)
        {
            throw new InvalidOperationException($"{operation} failed with native status {status}.");
        }
    }
}
