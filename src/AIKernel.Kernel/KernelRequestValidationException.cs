namespace AIKernel.Kernel;

public sealed class KernelRequestValidationException : Exception
{
    public KernelRequestValidationException(string message)
        : base(message)
    {
    }
}
