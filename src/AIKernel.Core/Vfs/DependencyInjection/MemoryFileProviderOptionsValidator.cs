namespace AIKernel.Core.Vfs.DependencyInjection;

using AIKernel.Core.Vfs.Memory;
using Microsoft.Extensions.Options;

internal sealed class MemoryFileProviderOptionsValidator : IValidateOptions<MemoryFileProviderOptions>
{
    public ValidateOptionsResult Validate(string? name, MemoryFileProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ProviderId))
        {
            return ValidateOptionsResult.Fail("MemoryFileProvider ProviderId is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Name))
        {
            return ValidateOptionsResult.Fail("MemoryFileProvider Name is required.");
        }

        return ValidateOptionsResult.Success;
    }
}
