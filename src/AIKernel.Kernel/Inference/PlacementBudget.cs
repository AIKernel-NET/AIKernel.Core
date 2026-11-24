using System.Globalization;

namespace AIKernel.Kernel.Inference;

internal static class PlacementBudget
{
    internal static long Resolve(long? explicitBytes)
    {
        long bytes = explicitBytes ?? ReadHostBudget();
        ArgumentOutOfRangeException.ThrowIfNegative(bytes);
        return bytes;
    }

    private static long ReadHostBudget()
    {
        string? value = Environment.GetEnvironmentVariable("AIKERNEL_DEVICE_BUDGET_BYTES");
        if (value is null) return 0;
        return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long bytes)
            ? bytes : throw new InvalidDataException("AIKERNEL_DEVICE_BUDGET_BYTES must be a nonnegative integer.");
    }
}
