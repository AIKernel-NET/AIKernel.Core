namespace AIKernel.Core.Vfs.DependencyInjection;

using AIKernel.Core.Vfs.Web;
using Microsoft.Extensions.Options;

internal sealed class WebGetFileProviderOptionsValidator : IValidateOptions<WebGetFileProviderOptions>
{
    /// <summary>
    /// EN: Executes Validate.
    /// EN: Documentation for public API. JA: Validate を実行します。
    /// </summary>
    public ValidateOptionsResult Validate(string? name, WebGetFileProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ProviderId))
        {
            return ValidateOptionsResult.Fail("WebGetFileProvider ProviderId is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Name))
        {
            return ValidateOptionsResult.Fail("WebGetFileProvider Name is required.");
        }

        if (options.BaseUri is null)
        {
            return ValidateOptionsResult.Fail("WebGetFileProvider BaseUri is required.");
        }

        if (options.BaseUri.Scheme is not "http" and not "https")
        {
            return ValidateOptionsResult.Fail("WebGetFileProvider BaseUri must use http or https.");
        }

        if (options.Timeout <= TimeSpan.Zero)
        {
            return ValidateOptionsResult.Fail("WebGetFileProvider Timeout must be greater than zero.");
        }

        return ValidateOptionsResult.Success;
    }
}
