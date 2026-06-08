namespace AIKernel.Common.Results;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.OptionWhereExtensions']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Results.OptionWhereExtensions']" />
public static class OptionWhereExtensions
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.OptionWhereExtensions.Where&lt;T&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Results.OptionWhereExtensions.Where&lt;T&gt;']" />
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
