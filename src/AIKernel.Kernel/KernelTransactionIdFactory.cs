namespace AIKernel.Kernel;

using AIKernel.Abstractions.Kernel;
using AIKernel.Dtos.Kernel;

/// <summary>[EN] Documents this public package API member. [JA] KernelTransactionIdFactory を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Kernel.KernelTransactionIdFactory']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Kernel.KernelTransactionIdFactory']/summary" />
public sealed class KernelTransactionIdFactory : IKernelTransactionIdFactory
{
    private readonly IKernelRequestHasher _requestHasher;

    /// <summary>[EN] Documents this public package API member. [JA] KernelTransactionIdFactory を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Kernel.KernelTransactionIdFactory.#ctor']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Kernel.KernelTransactionIdFactory.#ctor']/summary" />
    public KernelTransactionIdFactory(IKernelRequestHasher requestHasher)
    {
        _requestHasher = requestHasher
            ?? throw new ArgumentNullException(nameof(requestHasher));
    }

    /// <summary>[EN] Documents this public package API member. [JA] CreateTransactionId を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Kernel.KernelTransactionIdFactory.CreateTransactionId']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Kernel.KernelTransactionIdFactory.CreateTransactionId']/summary" />
    public string CreateTransactionId(KernelRequest request)
    {
        var requestHash = _requestHasher.ComputeHash(request);
        return $"ktx:{requestHash}";
    }
}
