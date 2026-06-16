namespace AIKernel.Core.Dsl;

using AIKernel.Common.Results;

internal sealed class FailClosedDslCapabilityRegistry : IDslCapabilityRegistry
{
    /// <summary>
    /// EN: Executes Contains.
    /// [EN] Documents this public package API member. [JA] Contains を実行します。
    /// </summary>
    public bool Contains(string name) => false;
    /// <summary>
    /// EN: Gets Invoke.
    /// [EN] Documents this public package API member. [JA] Invoke を取得します。
    /// </summary>

    public Result<DslPipelineValue> Invoke(
        string name,
        DslPipelineValue input,
        IReadOnlyDictionary<string, string> args)
        => Result<DslPipelineValue>.Fail(new ErrorContext(
            $"DSL capability is not registered: {name}",
            "DSL_CAPABILITY_NOT_REGISTERED",
            false)
        {
            FailureKind = FailureKind.FailClosed,
            OriginStep = OriginStep.Capability,
            SemanticSlot = SemanticSlot.T,
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["dsl.capability_name"] = name
            }
        });
}
