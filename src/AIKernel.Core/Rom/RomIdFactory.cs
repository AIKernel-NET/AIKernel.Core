namespace AIKernel.Core.Rom;

using AIKernel.Dtos.Rom;

public static class RomIdFactory
{
    public static RomId Create(string value, string? paramName = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("ROM id is required.", paramName ?? nameof(value));
        }

        return new RomId(value.Trim());
    }
}
