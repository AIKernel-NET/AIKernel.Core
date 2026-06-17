namespace AIKernel.Core.Rom;

using AIKernel.Dtos.Rom;

/// <summary>[EN] Documents this public package API member. [JA] RomIdFactory を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Rom.RomIdFactory']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Rom.RomIdFactory']/summary" />
public static class RomIdFactory
{
    /// <summary>[EN] Documents this public package API member. [JA] Create を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.RomIdFactory.Create']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.RomIdFactory.Create']/summary" />
    public static RomId Create(string value, string? paramName = null)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("ROM id is required.", paramName ?? nameof(value));
        }

        return new RomId(value.Trim());
    }
}
