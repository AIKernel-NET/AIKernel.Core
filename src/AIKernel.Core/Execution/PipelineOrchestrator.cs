namespace AIKernel.Core.Execution;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIKernel.Abstractions.Context;
using AIKernel.Abstractions.Execution;
using AIKernel.Dtos.Context;
using AIKernel.Dtos.Execution;
using AIKernel.Dtos.KernelContext;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.PipelineOrchestrator']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.PipelineOrchestrator']" />
public sealed class PipelineOrchestrator(IKernelReplayer replayer) : IPipelineOrchestrator
{
    private readonly IKernelReplayer _replayer =
        replayer ?? throw new ArgumentNullException(nameof(replayer));

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.PipelineOrchestrator.InitializeAsync']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.PipelineOrchestrator.InitializeAsync']" />
    public Task<InitializationResult> InitializeAsync(
        IContextCollection context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);

        return Task.FromResult(new InitializationResult
        {
            IsInitialized = true,
            Message = "Pipeline context initialized.",
            Issues = [],
            PreExecutionContextHash = ComputeContextHash(context)
        });
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.PipelineOrchestrator.ExecuteAsync']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.PipelineOrchestrator.ExecuteAsync']" />
    public Task<ExecutionResult> ExecuteAsync(
        IContextCollection context,
        SignatureVerificationResult signatureVerificationResult,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(signatureVerificationResult);

        if (!signatureVerificationResult.IsValid)
        {
            return Task.FromResult(CreateFailure(
                "Signature verification failed.",
                context));
        }

        return Task.FromResult(CreateFailure(
            "Pipeline execution requires a bound execution backend.",
            context));
    }

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.PipelineOrchestrator.ReplayFromDumpAsync']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Core.Execution.PipelineOrchestrator.ReplayFromDumpAsync']" />
    public async Task<ExecutionResult> ReplayFromDumpAsync(
        ReplayDump replayDump,
        ModificationContext modificationContext,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(modificationContext);

        return await _replayer.ReplayAsync(
            replayDump,
            CreateReplayTraceContext(modificationContext),
            cancellationToken).ConfigureAwait(false);
    }

    private static ExecutionResult CreateFailure(
        string message,
        IContextCollection context)
    {
        return new ExecutionResult
        {
            Logic = new RawLogic(ComputeContextHash(context)),
            FinalOutput = string.Empty,
            IsSuccessful = false,
            ErrorMessage = message,
            ElapsedMilliseconds = 0
        };
    }

    private static TraceContext CreateReplayTraceContext(
        ModificationContext modificationContext)
    {
        var reason = modificationContext.Reason ?? string.Empty;
        var targetPhase = modificationContext.TargetPhase ?? string.Empty;
        var modifiedBy = modificationContext.ModifiedBy ?? string.Empty;
        var traceId = ComputeHash(string.Join("\n", reason, targetPhase, modifiedBy));

        return new TraceContext(
            TraceId: traceId,
            SpanId: "pipeline-replay",
            ParentSpanId: string.Empty,
            StartTime: DateTime.UnixEpoch,
            EndTime: null,
            Tags: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["reason"] = reason,
                ["target_phase"] = targetPhase,
                ["modified_by"] = modifiedBy
            },
            Logs: []);
    }

    private static string ComputeContextHash(IContextCollection context)
    {
        var fragments = context.GetAll()
            .OrderBy(x => x.FragmentId ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(x => x.Category.ToString(), StringComparer.Ordinal)
            .Select(CreateCanonicalFragment)
            .ToArray();
        var canonical = JsonSerializer.Serialize(fragments, new JsonSerializerOptions
        {
            WriteIndented = false
        });

        return ComputeHash(canonical);
    }

    private static object CreateCanonicalFragment(ContextFragment fragment)
    {
        return new
        {
            fragment_id = fragment.FragmentId ?? string.Empty,
            category = fragment.Category.ToString(),
            content = Normalize(fragment.Content),
            priority = fragment.Priority,
            metadata = (fragment.Metadata ?? new Dictionary<string, string>())
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal)
        };
    }

    private static string ComputeHash(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var hash = SHA256.HashData(bytes);

        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string Normalize(string? value)
    {
        return (value ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Trim();
    }
}
