namespace AIKernel.Kernel;

using AIKernel.Abstractions.Kernel;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.Kernel;

public static class KernelRequestExecutionExtensions
{
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
