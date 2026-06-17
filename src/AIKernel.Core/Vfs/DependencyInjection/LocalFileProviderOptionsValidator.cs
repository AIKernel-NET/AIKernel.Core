namespace AIKernel.Core.Vfs.DependencyInjection;

using AIKernel.Core.Vfs.Local;
using Microsoft.Extensions.Options;

internal sealed class LocalFileProviderOptionsValidator : IValidateOptions<LocalFileProviderOptions>
{
    /// <summary>
    /// EN: Executes Validate.
    /// [EN] Documents this public package API member. [JA] Validate を実行します。
    /// </summary>
    public ValidateOptionsResult Validate(string? name, LocalFileProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ProviderId))
        {
            return ValidateOptionsResult.Fail("LocalFileProvider ProviderId is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Name))
        {
            return ValidateOptionsResult.Fail("LocalFileProvider Name is required.");
        }

        if (string.IsNullOrWhiteSpace(options.RootPath))
        {
            return ValidateOptionsResult.Fail("LocalFileProvider RootPath is required.");
        }

        return ValidateOptionsResult.Success;
    }
}
