namespace AIKernel.Core.Context;

using AIKernel.Dtos.Rom;

public sealed class RomIdentityMismatchException : ContextAssemblyException
{
    public RomIdentityMismatchException(RomId requested, RomId actual, string path)
        : base($"Loaded ROM identity did not match requested identity. Requested='{requested.Value}', Actual='{actual.Value}', Path='{path}'.")
    {
        Requested = requested;
        Actual = actual;
        Path = path;
    }

    public RomId Requested { get; }

    public RomId Actual { get; }

    public string Path { get; }
}
