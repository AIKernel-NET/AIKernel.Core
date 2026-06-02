namespace AIKernel.Kernel;

using AIKernel.Abstractions.Kernel;
using AIKernel.Dtos.Kernel;

public sealed class KernelTransactionIdFactory : IKernelTransactionIdFactory
{
    private readonly IKernelRequestHasher _requestHasher;
    private long _sequence;

    public KernelTransactionIdFactory(IKernelRequestHasher requestHasher)
    {
        _requestHasher = requestHasher
            ?? throw new ArgumentNullException(nameof(requestHasher));
    }

    public string CreateTransactionId(KernelRequest request)
    {
        var requestHash = _requestHasher.ComputeHash(request);
        var sequence = Interlocked.Increment(ref _sequence);

        return $"ktx:{requestHash}:{sequence:D8}";
    }
}