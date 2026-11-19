using System.Globalization;
using System.Numerics;

namespace AIKernel.Kernel.Inference;

// v26 experiment. Exact arithmetic for two already extracted literals; no statement
// about their entity, timestamp, authority, requested reporting format, or council vote.
internal sealed record BonsaiCtgQuantityRelation(string Relation, string? CandidateInObservationUnit, string Description)
{
    private readonly record struct Unit(string Dimension, int Power);
    private readonly record struct Number(BigInteger Coefficient, int Power);

    internal static BonsaiCtgQuantityRelation? TryCompare(string candidateValue, string candidateUnit,
        string observedValue, string observedUnit)
    {
        if (!TryUnit(candidateUnit, out var leftUnit) || !TryUnit(observedUnit, out var rightUnit) ||
            !TryNumber(candidateValue, out var leftNumber) || !TryNumber(observedValue, out var rightNumber)) return null;

        string first = $"The proposal gives {candidateValue} {candidateUnit}.";
        string second = $"The observation gives {observedValue} {observedUnit}.";
        if (leftUnit.Dimension != rightUnit.Dimension)
            return new("incompatible_dimensions", null,
                $"{first} {second} Their units describe different physical dimensions ({leftUnit.Dimension} and {rightUnit.Dimension}).");

        int convertedPower = leftNumber.Power + leftUnit.Power - rightUnit.Power;
        string converted = CanonicalDecimal(leftNumber.Coefficient, convertedPower);
        string observed = CanonicalDecimal(rightNumber.Coefficient, rightNumber.Power);
        bool equal = string.Equals(converted, observed, StringComparison.Ordinal);
        if (candidateUnit != observedUnit)
            first = $"The proposed {candidateValue} {candidateUnit} equals {converted} {observedUnit}.";
        string physical = equal ? "The two quantities are physically equal." : "The two quantities are physically different.";
        return new(equal ? "equal" : "different", converted, $"{first} {second} {physical}");
    }

    private static bool TryUnit(string symbol, out Unit unit)
    {
        unit = default;
        if (symbol is not { Length: >= 1 and <= 3 }) return false;
        // Treat the same Celsius scale as comparable. No offset conversions or
        // prefixes are implemented for temperature.
        if (symbol == "°C")
        {
            unit = new("temperature", 0);
            return true;
        }

        static string? Dimension(string mark) => mark switch
        {
            "m" => "length", "s" => "time", "g" => "mass", "V" => "potential",
            "A" => "current", "W" => "power", "Hz" => "frequency", "Pa" => "pressure", "L" => "volume",
            _ => null
        };
        string? dimension = Dimension(symbol);
        int power = 0;
        if (dimension is null)
        {
            int? prefix = symbol[0] switch
            {
                'M' => 6, 'k' => 3, 'c' => -2, 'm' => -3,
                'u' or 'µ' or 'μ' => -6, 'n' => -9, _ => null
            };
            if (!prefix.HasValue || (dimension = Dimension(symbol[1..])) is null) return false;
            power = prefix.Value;
        }
        unit = new(dimension, power);
        return true;
    }

    private static bool TryNumber(string value, out Number number)
    {
        number = default;
        if (value is not { Length: >= 1 and <= 42 }) return false;
        int first = value[0] is '+' or '-' ? 1 : 0;
        int dot = -1, digits = 0;
        for (int i = first; i < value.Length; i++)
        {
            if (char.IsAsciiDigit(value[i])) digits++;
            else if (value[i] == '.' && dot < 0 && i != first && i + 1 != value.Length) dot = i;
            else return false;
        }
        if (digits is < 1 or > 40) return false;
        string coefficient = dot < 0 ? value : value.Remove(dot, 1);
        if (!BigInteger.TryParse(coefficient, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var integer)) return false;
        number = new(integer, dot < 0 ? 0 : dot + 1 - value.Length);
        return true;
    }

    // Coefficient is an integer and Power is a bounded power of ten. Moving
    // the decimal point and trimming insignificant fractional zeros is exact.
    private static string CanonicalDecimal(BigInteger coefficient, int power)
    {
        if (coefficient.IsZero) return "0";
        string sign = coefficient.Sign < 0 ? "-" : "";
        string digits = BigInteger.Abs(coefficient).ToString(CultureInfo.InvariantCulture);
        if (power >= 0) return sign + digits + new string('0', power);
        int places = -power;
        digits = digits.PadLeft(places + 1, '0');
        return sign + digits.Insert(digits.Length - places, ".").TrimEnd('0').TrimEnd('.');
    }
}
