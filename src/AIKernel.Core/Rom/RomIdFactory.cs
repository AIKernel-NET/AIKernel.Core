namespace AIKernel.Core.Rom;

using AIKernel.Dtos.Rom;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Rom.RomIdFactory']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Rom.RomIdFactory']" />
public static class RomIdFactory
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.RomIdFactory.Create']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.RomIdFactory.Create']" />
    public static RomId Create(string value, string? paramName = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("ROM id is required.", paramName ?? nameof(value));
        }

        return new RomId(value.Trim());
    }
}
