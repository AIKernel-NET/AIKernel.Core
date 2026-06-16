namespace AIKernel.Core.Execution;

using AIKernel.Abstractions.Execution;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.KernelContext;

/// <summary>EN: Documentation for public API. JA: KernelReplayer を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.KernelReplayer']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.KernelReplayer']/summary" />
public sealed class KernelReplayer : IKernelReplayer
{
    /// <summary>EN: Documentation for public API. JA: CanReplay を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.KernelReplayer.CanReplay']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.KernelReplayer.CanReplay']/summary" />
    public bool CanReplay(ReplayDump replayDump)
    {
        return replayDump is
        {
            OriginalResult: not null,
            HashChain: not null
        }
        && !string.IsNullOrWhiteSpace(replayDump.DumpId)
        && !string.IsNullOrWhiteSpace(replayDump.HashChain.HashAlgorithm);
    }

    /// <summary>EN: Documentation for public API. JA: ReplayAsync を取得します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.KernelReplayer.ReplayAsync']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.KernelReplayer.ReplayAsync']/summary" />
    public ValueTask<ExecutionResult> ReplayAsync(
        ReplayDump replayDump,
        TraceContext traceContext,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(traceContext);

        if (!CanReplay(replayDump))
        {
            return ValueTask.FromResult(CreateFailure(
                "Replay dump is incomplete or fails closed.",
                replayDump?.StructureOutput));
        }

        var original = replayDump.OriginalResult;

        return ValueTask.FromResult(new ExecutionResult
        {
            Logic = replayDump.StructureOutput ?? original.Logic,
            FinalOutput = original.FinalOutput,
            IsSuccessful = original.IsSuccessful,
            ErrorMessage = original.ErrorMessage,
            ElapsedMilliseconds = original.ElapsedMilliseconds
        });
    }

    private static ExecutionResult CreateFailure(
        string message,
        RawLogic? logic)
    {
        return new ExecutionResult
        {
            Logic = logic ?? new RawLogic(string.Empty),
            FinalOutput = string.Empty,
            IsSuccessful = false,
            ErrorMessage = message,
            ElapsedMilliseconds = 0
        };
    }
}
