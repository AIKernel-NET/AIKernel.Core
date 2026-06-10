namespace AIKernel.Core.Rom;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Rom.RomRequiredMetadataMissingException']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Rom.RomRequiredMetadataMissingException']/summary" />
public sealed class RomRequiredMetadataMissingException : RomLoadException
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.RomRequiredMetadataMissingException.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.RomRequiredMetadataMissingException.#ctor']/summary" />
    public RomRequiredMetadataMissingException(string metadataKey, string sourcePath)
        : base($"Required ROM metadata '{metadataKey}' is missing. SourcePath='{sourcePath}'.")
    {
        MetadataKey = metadataKey;
        SourcePath = sourcePath;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Rom.RomRequiredMetadataMissingException.MetadataKey']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Rom.RomRequiredMetadataMissingException.MetadataKey']/summary" />
    public string MetadataKey { get; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Rom.RomRequiredMetadataMissingException.SourcePath']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Rom.RomRequiredMetadataMissingException.SourcePath']/summary" />
    public string SourcePath { get; }
}