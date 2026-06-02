#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Abstractions.Kernel;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

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

        throw new NotSupportedException(
            "This IKernel implementation does not expose KernelRequest execution.");
    }
}
