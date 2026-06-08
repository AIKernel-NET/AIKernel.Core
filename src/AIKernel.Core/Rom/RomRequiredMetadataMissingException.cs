namespace AIKernel.Core.Rom;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Rom.RomRequiredMetadataMissingException']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Rom.RomRequiredMetadataMissingException']" />
public sealed class RomRequiredMetadataMissingException : RomLoadException
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.RomRequiredMetadataMissingException.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Rom.RomRequiredMetadataMissingException.#ctor']" />
    public RomRequiredMetadataMissingException(string metadataKey, string sourcePath)
        : base($"Required ROM metadata '{metadataKey}' is missing. SourcePath='{sourcePath}'.")
    {
        MetadataKey = metadataKey;
        SourcePath = sourcePath;
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Rom.RomRequiredMetadataMissingException.MetadataKey']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Rom.RomRequiredMetadataMissingException.MetadataKey']" />
    public string MetadataKey { get; }

    /// <include file="docs.en.xml" path="doc/members/member[@name='P:AIKernel.Core.Rom.RomRequiredMetadataMissingException.SourcePath']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='P:AIKernel.Core.Rom.RomRequiredMetadataMissingException.SourcePath']" />
    public string SourcePath { get; }
}