#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Dtos.Rom;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

using System.Collections.Immutable;

public sealed record RomId(string Value)
{
    public static RomId Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("rom_id is required.", nameof(value));
        }

        return new RomId(value.Trim());
    }

    public override string ToString() => Value;
}