namespace AIKernel.Common.Results;

public static class OptionWhereExtensions
{
    public static Option<T> Where<T>(
        this Option<T> option,
        Func<T, bool> predicate)
    {
        if (!option.HasValue)
            return option;

        return predicate(option.Value!)
            ? option
            : Option<T>.None();
    }
}
