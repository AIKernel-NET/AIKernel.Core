namespace AIKernel.Common.Results;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.OptionWhereExtensions']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.OptionWhereExtensions']/summary" />
public static class OptionWhereExtensions
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.OptionWhereExtensions.Where&lt;T&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.OptionWhereExtensions.Where&lt;T&gt;']/summary" />
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
