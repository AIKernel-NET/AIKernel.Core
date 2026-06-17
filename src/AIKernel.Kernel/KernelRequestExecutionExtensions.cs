namespace AIKernel.Kernel;

using AIKernel.Abstractions.Kernel;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.Kernel;

/// <summary>[EN] Documents this public package API member. [JA] KernelRequestExecutionExtensions を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Kernel.KernelRequestExecutionExtensions']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Kernel.KernelRequestExecutionExtensions']/summary" />
public static class KernelRequestExecutionExtensions
{
    /// <summary>[EN] Documents this public package API member. [JA] ExecuteAsync を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Kernel.KernelRequestExecutionExtensions.ExecuteAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Kernel.KernelRequestExecutionExtensions.ExecuteAsync']/summary" />
    public static Task<KernelRequestExecutionResult> ExecuteAsync(
        this IKernel kernel,
        KernelRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(kernel);

        if (kernel is AIKernel.Kernel.Kernel coreKernel)
        {
            return coreKernel.ExecuteAsync(request, cancellationToken);
        }

        throw new InvalidOperationException(
            "This IKernel implementation does not expose KernelRequest execution.");
    }
}
