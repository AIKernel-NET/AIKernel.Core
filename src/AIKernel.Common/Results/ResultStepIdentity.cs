namespace AIKernel.Common.Results;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

internal static class ResultStepIdentity
{
    public static string Create(
        string? parentStepId,
        SemanticDelta delta,
        bool isSuccess,
        string? errorCode)
    {
        var payload = BuildCanonicalPayload(
            parentStepId,
            delta,
            isSuccess,
            errorCode);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));

        return "step:sha256:" + Convert.ToHexStringLower(bytes);
    }

    private static string BuildCanonicalPayload(
        string? parentStepId,
        SemanticDelta delta,
        bool isSuccess,
        string? errorCode)
    {
        var builder = new StringBuilder();
        builder.AppendLine("aikernel.result-step.v1");
        builder.AppendLine(parentStepId ?? string.Empty);
        builder.AppendLine(isSuccess ? "success" : "failure");
        builder.AppendLine(errorCode ?? string.Empty);
        builder.AppendLine(delta.Label);
        builder.AppendLine(delta.OriginStep?.ToString() ?? string.Empty);
        builder.AppendLine(delta.SemanticSlot?.ToString() ?? string.Empty);

        if (delta.Metadata is not null)
        {
            foreach (var item in delta.Metadata.OrderBy(
                item => item.Key,
                StringComparer.Ordinal))
            {
                builder.Append(item.Key);
                builder.Append('=');
                builder.AppendLine(item.Value);
            }
        }

        builder.AppendLine(
            delta.Metadata?.Count.ToString(CultureInfo.InvariantCulture) ?? "0");

        return builder.ToString();
    }
}
