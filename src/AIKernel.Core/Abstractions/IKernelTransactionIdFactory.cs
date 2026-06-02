#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Abstractions.Kernel;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

using AIKernel.Dtos.Kernel;

public interface IKernelTransactionIdFactory
{
    string CreateTransactionId(KernelRequest request);
}