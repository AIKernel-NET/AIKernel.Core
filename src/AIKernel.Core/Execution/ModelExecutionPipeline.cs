namespace AIKernel.Core.Execution;

using System.Collections.Immutable;
using System.Text.Json;
using AIKernel.Abstractions.Execution;
using AIKernel.Abstractions.Providers;
using AIKernel.Core.Context;
using AIKernel.Core.Time;
using AIKernel.Dtos.Context;
using AIKernel.Dtos.Execution;
using AIKernel.Enums;

internal sealed class ModelExecutionPipeline(
    IModelProvider provider,
    string modelId,
    IKernelExecutor executor,
    IOutputPolisher polisher,
    IPolisherValidator validator,
    IKernelClock clock)
{
    private const string StructureInstruction =
        "Build a plan for the supplied task using the provided material. Preserve the facts needed to write the answer. " +
        "Return only a JSON object with a 'steps' array of non-empty strings.";

    private const string GenerationInstruction =
        "Write the final answer from the supplied plan. Preserve its facts and conclusions. " +
        "Treat the plan as source material and do not add facts beyond it.";

    public async Task<(ExecutionResult Result, ReplayDump? Dump)> ExecuteAsync(
        ContextCollectionSnapshot snapshot,
        string contextHash,
        bool captureReplay,
        CancellationToken cancellationToken)
    {
        var started = clock.Now;
        var input = snapshot.GetAll()
            .Where(fragment => fragment.Category is ContextCategory.Orchestration or ContextCategory.Material)
            .Where(fragment => !string.IsNullOrWhiteSpace(fragment.Content))
            .ToArray();
        if (!input.Any(fragment => fragment.Category == ContextCategory.Orchestration))
        {
            return Failure("Structure requires an orchestration fragment.");
        }

        var expression = CaptureExpression(snapshot);
        var stage = "Structure";
        try
        {
            if (!await provider.IsAvailableAsync().ConfigureAwait(false))
            {
                return Failure("The configured pipeline model provider is unavailable.");
            }

            cancellationToken.ThrowIfCancellationRequested();
            var structureResult = await RunPhaseAsync(contextHash, StructureInstruction, input.Select(fragment =>
                new ContextPromptBlock(fragment.FragmentId, fragment.Category.ToString(), fragment.Content,
                    fragment.Category == ContextCategory.Orchestration ? 900 : 700,
                    ImmutableDictionary<string, string>.Empty)).ToArray(), cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (structureResult.Status != ExecutionStatus.Succeeded ||
                !IsValidStructure(structureResult.OutputText))
            {
                return Failure("Structure did not produce a valid execution plan.");
            }

            var structure = new RawLogic(structureResult.OutputText!);
            stage = "Generation";
            var generationResult = await RunPhaseAsync(
                PipelineOrchestrator.ComputeHash(structure.SerializedRepresentation),
                GenerationInstruction,
                [new ContextPromptBlock("pipeline.structure", nameof(ContextCategory.Material),
                    structure.SerializedRepresentation, 700, ImmutableDictionary<string, string>.Empty)],
                cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (generationResult.Status != ExecutionStatus.Succeeded ||
                string.IsNullOrWhiteSpace(generationResult.OutputText))
            {
                return Failure("Generation did not produce an answer.");
            }

            stage = "Polish";
            var generated = new RawLogic(generationResult.OutputText);
            var final = await polisher.RenderAsync(generated,
                expression, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(final))
            {
                return Failure("Polish did not produce an answer.");
            }

            var validation = await validator.ValidateLogicPreservationAsync(
                generated, final, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!validation.IsValid)
            {
                return Failure("Polish altered the generated answer.");
            }

            var result = new ExecutionResult
            {
                Logic = structure,
                FinalOutput = final,
                IsSuccessful = true,
                ElapsedMilliseconds = Elapsed()
            };

            if (!captureReplay)
            {
                return (result, null);
            }

            var dumpId = "pipeline:" + PipelineOrchestrator.ComputeHash(string.Join("\n",
                contextHash, structureResult.ExecutionId, generationResult.ExecutionId, PipelineOrchestrator.ComputeHash(final)));
            var createdAt = clock.Now.UtcDateTime;
            return (result, new ReplayDump
            {
                DumpId = dumpId,
                CreatedAt = createdAt,
                StructureOutput = structure,
                GenerationOutput = generated.SerializedRepresentation,
                OriginalResult = result,
                HashChain = ReplayDumpIntegrity.CreateHashChain(
                    dumpId, createdAt, structure, generated.SerializedRepresentation, result)
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Failure($"{stage} execution failed.");
        }

        long Elapsed() => Math.Max(0, (long)(clock.Now - started).TotalMilliseconds);
        (ExecutionResult, ReplayDump?) Failure(string message) =>
            (PipelineOrchestrator.CreateFailure(message, contextHash, Elapsed()), null);
    }

    private static ExpressionContext CaptureExpression(ContextCollectionSnapshot snapshot)
        => new(new ExpressionBuffer(snapshot.GetExpressionBuffer().Fragments.Select(fragment => new ExpressionFragment
        {
            FragmentId = fragment.FragmentId,
            Content = fragment.Content,
            Priority = fragment.Priority,
            CreatedAt = fragment.CreatedAt,
            Metadata = fragment.Metadata?.ToImmutableDictionary(StringComparer.Ordinal),
            Examples = fragment.Examples.ToImmutableArray(),
            StyleHints = fragment.StyleHints,
            Tone = fragment.Tone,
            LanguageTag = fragment.LanguageTag
        })));

    private Task<KernelRequestExecutionResult> RunPhaseAsync(
        string inputHash,
        string instruction,
        IReadOnlyList<ContextPromptBlock> context,
        CancellationToken cancellationToken)
        => executor.ExecuteAsync(provider, new KernelExecutionRequest
        {
            ContextSnapshotId = inputHash,
            ContextHash = inputHash,
            ContextBlocks = context,
            UserInstruction = instruction,
            RequestedModelId = modelId,
            PromptOptions = new PromptGenerationOptions
            {
                OverflowPolicy = PromptOverflowPolicy.FailClosed,
                IncludeContextHash = false,
                IncludeSourceMetadata = false
            },
            ExecutionOptions = new ExecutionOptions
            {
                Temperature = 0,
                TopP = 1,
                MaxOutputTokens = null,
                StopSequences = []
            }
        }, cancellationToken);

    private static bool IsValidStructure(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            return root.ValueKind == JsonValueKind.Object &&
                root.EnumerateObject().Count() == 1 &&
                root.TryGetProperty("steps", out var steps) &&
                steps.ValueKind == JsonValueKind.Array &&
                steps.GetArrayLength() > 0 &&
                steps.EnumerateArray().All(step =>
                    step.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(step.GetString()));
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
