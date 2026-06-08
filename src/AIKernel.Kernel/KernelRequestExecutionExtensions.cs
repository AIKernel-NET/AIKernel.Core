namespace AIKernel.Kernel;

using AIKernel.Abstractions.Kernel;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.Kernel;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Kernel.KernelRequestExecutionExtensions']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Kernel.KernelRequestExecutionExtensions']" />
public static class KernelRequestExecutionExtensions
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Kernel.KernelRequestExecutionExtensions.ExecuteAsync']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Kernel.KernelRequestExecutionExtensions.ExecuteAsync']" />
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
