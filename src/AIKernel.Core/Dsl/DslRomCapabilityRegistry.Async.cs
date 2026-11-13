namespace AIKernel.Core.Dsl;

using AIKernel.Common.Results;

internal sealed partial class DslRomCapabilityRegistry
{
    public async Task<Result<DslPipelineValue>> InvokeAsync(
        string name,
        DslPipelineValue input,
        IReadOnlyDictionary<string, string> args,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.CompletedTask.ConfigureAwait(ConfigureAwaitOptions.ForceYielding);
        cancellationToken.ThrowIfCancellationRequested();
        if (!DslRomPath.IsDslCapability(name))
        {
            Result<DslPipelineValue> result;
            try { result = await _inner.InvokeAsync(name, input, args, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception)
            {
                result = Result<DslPipelineValue>.Fail(CapabilityException(name, ErrorContext.FromException(exception)));
            }
            cancellationToken.ThrowIfCancellationRequested();
            return result;
        }

        var snapshot = from _ in ParseRequestedCapabilityName(name)
                       from resolved in ResolveSnapshot(name)
                       from validated in ValidateSnapshot(name, resolved)
                       select validated;
        cancellationToken.ThrowIfCancellationRequested();
        if (snapshot.IsFailure) return Result<DslPipelineValue>.Fail(snapshot.Error!);

        var value = snapshot.Value!;
        ResultStep<DslPipelineState, DslPipelineValue> executed;
        try
        {
            executed = await value.Pipeline.ExecuteAsync(DslPipelineExecutionContext.Create(input), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception exception)
        {
            return Result<DslPipelineValue>.Fail(AttachRomMetadata(ErrorContext.FromException(exception) with
            {
                FailureKind = FailureKind.FailClosed,
                OriginStep = OriginStep.Capability,
                SemanticSlot = SemanticSlot.T
            }, value.Metadata));
        }
        cancellationToken.ThrowIfCancellationRequested();
        return RequirePipelineValue(value.Metadata, executed).Map(output => AttachRomData(
            output, value.Metadata, executed.ReplayLog.Count, executed.ReplayLogHash));
    }
}
