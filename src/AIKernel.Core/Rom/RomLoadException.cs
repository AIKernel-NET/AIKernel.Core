namespace AIKernel.Core.Rom;

public class RomLoadException : Exception
{
    public RomLoadException(string message)
        : base(message)
    {
    }

    public RomLoadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}