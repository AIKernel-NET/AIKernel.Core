namespace AIKernel.Kernel;

using AIKernel.Abstractions.Security;
using AIKernel.Dtos.Context;
using AIKernel.Dtos.Security;

internal sealed class FailClosedPdp : IPdp
{
    /// <summary>
    /// EN: Executes Instance.
    /// EN: Documentation for public API. JA: Instance を実行します。
    /// </summary>
    public static FailClosedPdp Instance { get; } = new();

    private FailClosedPdp()
    {
    }
    /// <summary>
    /// EN: Gets EvaluateAsync.
    /// EN: Documentation for public API. JA: EvaluateAsync を取得します。
    /// </summary>

    public Task<AccessDecision> EvaluateAsync(
        AccessRequest request)
    {
        return Task.FromResult(new AccessDecision
        {
            Allowed = false,
            Reason = "No PDP policy is registered."
        });
    }
    /// <summary>
    /// EN: Gets AddPolicy.
    /// EN: Documentation for public API. JA: AddPolicy を取得します。
    /// </summary>

    public void AddPolicy(
        IPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
    }
    /// <summary>
    /// EN: Gets RemovePolicy.
    /// EN: Documentation for public API. JA: RemovePolicy を取得します。
    /// </summary>

    public bool RemovePolicy(
        string policyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyId);

        return false;
    }
    /// <summary>
    /// EN: Executes GetPolicies.
    /// EN: Documentation for public API. JA: GetPolicies を実行します。
    /// </summary>

    public IReadOnlyList<IPolicy> GetPolicies()
    {
        return [];
    }
    /// <summary>
    /// EN: Gets EvaluatePoliciesAsync.
    /// EN: Documentation for public API. JA: EvaluatePoliciesAsync を取得します。
    /// </summary>

    public Task<PolicyEvaluationResult> EvaluatePoliciesAsync(
        UnifiedContextDto contract)
    {
        return Task.FromResult(new PolicyEvaluationResult
        {
            AllAllowed = false,
            RiskLevel = "High"
        });
    }
}
