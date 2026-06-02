namespace AIKernel.Kernel;

using AIKernel.Abstractions.Security;
using AIKernel.Dtos.Context;
using AIKernel.Dtos.Security;

internal sealed class FailClosedPdp : IPdp
{
    public static FailClosedPdp Instance { get; } = new();

    private FailClosedPdp()
    {
    }

    public Task<AccessDecision> EvaluateAsync(
        AccessRequest request)
    {
        return Task.FromResult(new AccessDecision
        {
            Allowed = false,
            Reason = "No PDP policy is registered."
        });
    }

    public void AddPolicy(
        IPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);
    }

    public bool RemovePolicy(
        string policyId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(policyId);

        return false;
    }

    public IReadOnlyList<IPolicy> GetPolicies()
    {
        return [];
    }

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
