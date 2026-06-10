namespace AIKernel.Core.Routing;

using AIKernel.Abstractions.Routing;
using AIKernel.Common.Results;

/// <summary>
/// [EN] Minimal fail-closed semantic router used until provider-backed routing is installed.
/// [JA] provider-backed routing が導入されるまで使用する最小 fail-closed semantic router です。
/// </summary>
public sealed class PassThroughSemanticRouter : ISemanticRouter
{
    /// <summary>[EN] Routes input to the default local route. [JA] input を default local route へ route します。</summary>
    public Task<RouteResult> RouteAsync(string input)
        => Task.FromResult(new RouteResult(
            "local.default",
            Confidence(input),
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["router"] = "pass-through"
            }));

    private static double Confidence(string input)
        => RouteInput(input).Match(_ => 0.0, _ => 1.0);

    private static Either<string, string> RouteInput(string input)
        => string.IsNullOrWhiteSpace(input)
            ? Either<string, string>.FromLeft("empty")
            : Either<string, string>.FromRight(input);
}
