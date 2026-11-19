using AIKernel.Common.Results;
using AIKernel.Core.Memory;

namespace AIKernel.Kernel.Memory;

internal static class SharedMemoryChannel
{
    internal static bool IsValid(string name, long capacity)
    {
        if (name is null || name.Length is < 1 or > 200 || capacity < 1 || (ulong)capacity > (ulong)nint.MaxValue)
            return false;
        foreach (var c in name)
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('_' or '-' or '.')) return false;
        return name is not ("." or "..");
    }

    internal static Result<IMemoryRegion> Fail(string message) =>
        Result<IMemoryRegion>.Fail(new ErrorContext(message, "shared_memory_unavailable", false)
        { FailureKind = FailureKind.FailClosed });
}
