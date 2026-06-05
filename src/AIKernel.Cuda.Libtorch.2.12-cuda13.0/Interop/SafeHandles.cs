namespace AIKernel.Cuda.Libtorch.Cuda13.Interop;

using Microsoft.Win32.SafeHandles;

public sealed class SafeLlamaModelHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeLlamaModelHandle()
        : base(ownsHandle: true)
    {
    }

    internal SafeLlamaModelHandle(
        int nativeHandle)
        : base(ownsHandle: true)
    {
        SetHandle(new IntPtr(nativeHandle));
    }

    public int ModelHandle => handle.ToInt32();

    protected override bool ReleaseHandle()
    {
        try
        {
            return NativeMethods.UnloadModel(ModelHandle) == NativeStatus.Success;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }
}
