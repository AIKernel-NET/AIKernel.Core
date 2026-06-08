namespace AIKernel.Kernel;

using AIKernel.Abstractions.Kernel;
using AIKernel.Dtos.Kernel;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Kernel.KernelTransactionIdFactory']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Kernel.KernelTransactionIdFactory']" />
public sealed class KernelTransactionIdFactory : IKernelTransactionIdFactory
{
    private readonly IKernelRequestHasher _requestHasher;

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Kernel.KernelTransactionIdFactory.#ctor']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Kernel.KernelTransactionIdFactory.#ctor']" />
    public KernelTransactionIdFactory(IKernelRequestHasher requestHasher)
    {
        _requestHasher = requestHasher
            ?? throw new ArgumentNullException(nameof(requestHasher));
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Kernel.KernelTransactionIdFactory.CreateTransactionId']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Kernel.KernelTransactionIdFactory.CreateTransactionId']" />
    public string CreateTransactionId(KernelRequest request)
    {
        var requestHash = _requestHasher.ComputeHash(request);
        return $"ktx:{requestHash}";
    }
}
