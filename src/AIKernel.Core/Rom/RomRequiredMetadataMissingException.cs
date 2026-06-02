namespace AIKernel.Core.Rom;

public sealed class RomRequiredMetadataMissingException : RomLoadException
{
    public RomRequiredMetadataMissingException(string metadataKey, string sourcePath)
        : base($"Required ROM metadata '{metadataKey}' is missing. SourcePath='{sourcePath}'.")
    {
        MetadataKey = metadataKey;
        SourcePath = sourcePath;
    }

    public string MetadataKey { get; }

    public string SourcePath { get; }
}