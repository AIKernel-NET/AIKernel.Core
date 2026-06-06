namespace AIKernel.Core.Rom;

public sealed class RomSignatureVerificationException : RomLoadException
{
    public RomSignatureVerificationException(
        string sourcePath,
        string expectedHash,
        string actualHash)
        : base(
            $"ROM signature verification failed. SourcePath='{sourcePath}', Expected='{expectedHash}', Actual='{actualHash}'.")
    {
        SourcePath = sourcePath;
        ExpectedHash = expectedHash;
        ActualHash = actualHash;
    }

    public string SourcePath { get; }

    public string ExpectedHash { get; }

    public string ActualHash { get; }
}