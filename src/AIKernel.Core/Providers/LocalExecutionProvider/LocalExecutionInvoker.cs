namespace AIKernel.Core.Providers.LocalExecutionProvider;

using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIKernel.Abstractions.Capabilities;
using AIKernel.Abstractions.Dsl;
using AIKernel.Common.Results;
using AIKernel.Dtos.Capabilities;
using AIKernel.Dtos.Dsl;
using AIKernel.Enums;

/// <summary>
/// [EN] Inline invoker for deterministic local DSL pipeline execution.
/// [JA] 決定論的な local DSL pipeline 実行を行う inline invoker です。
/// </summary>
public sealed class LocalExecutionInvoker : ICapabilityModuleInvoker
{
    private readonly IDslPipelineCompiler _compiler;

    /// <summary>
    /// [EN] Creates an invoker using the existing Core DSL compiler/runtime.
    /// [JA] 既存 Core DSL compiler/runtime を使用する invoker を作成します。
    /// </summary>
    public LocalExecutionInvoker(
        IDslPipelineCompiler compiler)
    {
        _compiler = compiler ?? throw new ArgumentNullException(nameof(compiler));
    }

    /// <summary>
    /// [EN] Executes the InvokeAsync operation as part of the AIKernel public reference surface.
    /// [JA] AIKernel の公開参照サーフェスとして InvokeAsync 操作を実行します。
    /// </summary>
    public async ValueTask<CapabilityInvocationResult> InvokeAsync(
        CapabilityInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);

        var metadata = new Dictionary<string, string>(
            request.Metadata,
            StringComparer.Ordinal)
        {
            ["provider"] = LocalExecutionProvider.ProviderIdValue,
            ["operation"] = request.Operation
        };

        if (!string.Equals(request.CapabilityId, "aikernel.local.execute", StringComparison.Ordinal) ||
            !string.Equals(request.Operation, "pipeline.execute", StringComparison.Ordinal))
        {
            return Fail(request, metadata, "LOCAL_EXECUTION_UNSUPPORTED_OPERATION",
                "LocalExecutionInvoker only supports aikernel.local.execute / pipeline.execute.");
        }

        return await GetPipelineJson(request)
            .Match(
                () => Task.FromResult(Fail(
                    request,
                    metadata,
                    "LOCAL_EXECUTION_PIPELINE_REQUIRED",
                    "pipeline.json argument or metadata value is required.")),
                async pipelineJson =>
                {
                    var execution = await (
                        from document in ParseDocument(pipelineJson).AsTask()
                        from pipeline in CompilePipelineAsync(document, cancellationToken)
                        from executed in ExecutePipelineAsync(pipeline, ReadInput(request), cancellationToken)
                        select executed)
                        .ConfigureAwait(false);

                    return execution.Match(
                        error => Fail(
                            request,
                            metadata,
                            error.Code ?? "LOCAL_EXECUTION_FAILED",
                            error.Message ?? "Local DSL execution failed."),
                        result => CreateInvocationResult(request, metadata, result));
                })
            .ConfigureAwait(false);
    }

    private static CapabilityInvocationResult CreateInvocationResult(
        CapabilityInvocationRequest request,
        Dictionary<string, string> metadata,
        DslPipelineExecutionResult result)
    {

        metadata["dsl.status"] = result.Status.ToString();
        metadata["dsl.current_node"] = result.State.CurrentNode;
        metadata["dsl.executed_node_count"] = result.State.ExecutedNodeCount.ToString(
            System.Globalization.CultureInfo.InvariantCulture);
        metadata["dsl.replay_log_count"] = result.ReplayLogCount.ToString(
            System.Globalization.CultureInfo.InvariantCulture);

        foreach (var item in result.Output.Data.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            metadata[$"output.{item.Key}"] = item.Value;
        }

        var succeeded = result.Status == ExecutionStatus.Succeeded;
        return new CapabilityInvocationResult(
            request.InvocationId,
            request.CapabilityId,
            succeeded,
            OutputHash: ComputeHash(result),
            ErrorCode: GetExecutionErrorCode(result),
            ErrorMessage: GetExecutionErrorMessage(result),
            ReplayLogHash: result.ReplayLogHash,
            Metadata: metadata
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal));
    }

    private static Option<string> GetPipelineJson(
        CapabilityInvocationRequest request)
        => ReadNonEmptyValue(request.Arguments, "pipeline.json")
            .OrElseOption(ReadNonEmptyValue(request.Metadata, "pipeline.json"));

    private static DslPipelineValue ReadInput(
        CapabilityInvocationRequest request)
    {
        var input = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var item in request.Arguments.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            if (item.Key.StartsWith("input.", StringComparison.Ordinal))
            {
                input[item.Key["input.".Length..]] = item.Value;
            }
        }

        return new DslPipelineValue(input);
    }

    private async Task<Result<IKernelPipeline>> CompilePipelineAsync(
        DslDocument document,
        CancellationToken cancellationToken)
        => (await Try
                .RunAsync(() => _compiler.CompileAsync(document, cancellationToken))
                .ConfigureAwait(false))
            .MapError("LOCAL_EXECUTION_COMPILE_FAILED");

    private static async Task<Result<DslPipelineExecutionResult>> ExecutePipelineAsync(
        IKernelPipeline pipeline,
        DslPipelineValue input,
        CancellationToken cancellationToken)
        => (await Try
                .RunAsync(() => pipeline.ExecuteAsync(
                    new DslPipelineExecutionContext(input, DateTimeOffset.UnixEpoch),
                    cancellationToken))
                .ConfigureAwait(false))
            .MapError("LOCAL_EXECUTION_EXECUTE_FAILED");

    private static Result<DslDocument> ParseDocument(
        string json)
        => Try
            .Run(() =>
            {
                using var document = JsonDocument.Parse(json);
                return ParseNode(document.RootElement)
                .Bind(RequirePipelineRoot)
                .Map(node => new DslDocument(node));
            })
            .Bind(x => x)
            .MapError("LOCAL_EXECUTION_INVALID_PIPELINE", FailureKind.Reject);

    private static Result<PipelineNode> ParseNode(
        JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return Invalid<PipelineNode>("DSL node must be a JSON object.");
        }

        return ReadRequiredString(element, "type")
            .Bind(type => ParseTypedNode(type, element));
    }

    private static Result<PipelineNode> ParseTypedNode(
        string type,
        JsonElement element)
    {
        return type switch
        {
            "Pipeline" => ParsePipeline(element),
            "Step" => ParseStep(element),
            "CallCapability" => ParseCallCapability(element),
            "Loop" => ParseLoop(element),
            "LoopUntil" => ParseLoopUntil(element),
            "Suspend" => ParseSuspend(element),
            _ => Invalid<PipelineNode>($"Unsupported DSL node type: {type}.")
        };
    }

    private static Result<PipelineNode> RequirePipelineRoot(
        PipelineNode node)
        => node is PipelineRootNode
            ? Result<PipelineNode>.Success(node)
            : Invalid<PipelineNode>("DSL root node must be a Pipeline.");

    private static Result<PipelineNode> ParsePipeline(
        JsonElement element)
        => ReadNodeArray(element, "steps")
            .Map<PipelineNode>(steps => new PipelineRootNode(steps));

    private static Result<PipelineNode> ParseStep(
        JsonElement element)
        => ReadRequiredString(element, "name")
            .Map<PipelineNode>(name => new StepNode(name));

    private static Result<PipelineNode> ParseCallCapability(
        JsonElement element)
        => from name in ReadRequiredString(element, "name")
           from args in ReadArgs(element)
           select (PipelineNode)new CallCapabilityNode(name, args);

    private static Result<PipelineNode> ParseLoop(
        JsonElement element)
        => from maxIterations in ReadRequiredInt(element, "maxIterations")
           from body in ReadNodeArray(element, "body")
           select (PipelineNode)new LoopNode(maxIterations, body);

    private static Result<PipelineNode> ParseLoopUntil(
        JsonElement element)
        => from timeout in ReadTimeout(element, "timeout")
           from maxIterations in ReadRequiredInt(element, "maxIterations")
           from body in ReadNodeArray(element, "body")
           select (PipelineNode)new LoopUntilNode(timeout, maxIterations, body);

    private static Result<PipelineNode> ParseSuspend(
        JsonElement element)
        => ReadRequiredString(element, "reason")
            .Map<PipelineNode>(reason => new SuspendNode(reason));

    private static Result<IReadOnlyList<PipelineNode>> ReadNodeArray(
        JsonElement element,
        string propertyName)
    {
        var array = from property in RequireProperty(element, propertyName)
                    from valid in RequireValueKind(
                        property,
                        JsonValueKind.Array,
                        $"{propertyName} must be an array.")
                    select valid;

        return array.Match(
            Invalid<IReadOnlyList<PipelineNode>>,
            valid => valid
                .EnumerateArray()
                .Aggregate(
                    Result<ImmutableArray<PipelineNode>>.Success(
                        ImmutableArray<PipelineNode>.Empty),
                    (current, child) =>
                        from nodes in current
                        from node in ParseNode(child)
                        select nodes.Add(node))
                .Map<IReadOnlyList<PipelineNode>>(nodes => nodes));
    }

    private static Result<IReadOnlyDictionary<string, string>> ReadArgs(
        JsonElement element)
        => ReadProperty(element, "args")
            .Match(
                () => Result<IReadOnlyDictionary<string, string>>.Success(
                    new Dictionary<string, string>(StringComparer.Ordinal)),
                ReadArgsObject);

    private static Result<IReadOnlyDictionary<string, string>> ReadArgsObject(
        JsonElement args)
    {
        if (args.ValueKind != JsonValueKind.Object)
        {
            return Invalid<IReadOnlyDictionary<string, string>>("args must be an object.");
        }

        return args
            .EnumerateObject()
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .Aggregate(
                Result<ImmutableDictionary<string, string>>.Success(
                    ImmutableDictionary<string, string>.Empty),
                (current, property) =>
                    from values in current
                    from parsed in ReadArg(property)
                    select values.SetItem(parsed.Key, parsed.Value))
            .Map<IReadOnlyDictionary<string, string>>(values => values);
    }

    private static Result<KeyValuePair<string, string>> ReadArg(
        JsonProperty property)
        => ReadJsonString(property.Value)
            .Match(
                _ => property.Value.GetRawText(),
                value => value)
            .ToKeyValue(property.Name);

    private static Either<string, string> ReadJsonString(
        JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            return Either<string, string>.FromRight(value.GetString() ?? string.Empty);
        }

        return Either<string, string>.FromLeft("JSON value is not a string.");
    }

    private static Result<string> ReadRequiredString(
        JsonElement element,
        string propertyName)
    {
        var value = from property in RequireProperty(element, propertyName)
                    from valid in RequireValueKind(
                        property,
                        JsonValueKind.String,
                        $"{propertyName} must be a non-empty string.")
                    from text in RequireNonEmpty(
                        valid.GetString(),
                        $"{propertyName} must be a non-empty string.")
                    select text;

        return value.ToLocalExecutionResult();
    }

    private static Result<int> ReadRequiredInt(
        JsonElement element,
        string propertyName)
    {
        var value = from property in RequireProperty(element, propertyName)
                    from valid in RequireValueKind(
                        property,
                        JsonValueKind.Number,
                        $"{propertyName} must be an integer.")
                    from integer in ReadInt32(valid, $"{propertyName} must be an integer.")
                    select integer;

        return value.ToLocalExecutionResult();
    }

    private static Result<TimeSpan> ReadTimeout(
        JsonElement element,
        string propertyName)
        => ReadProperty(element, propertyName)
            .Match(
                () => Invalid<TimeSpan>($"{propertyName} is required."),
                property => ReadTimeoutValue(property, propertyName));

    private static Result<TimeSpan> ReadTimeoutValue(
        JsonElement property,
        string propertyName)
    {
        if (property.ValueKind == JsonValueKind.Number &&
            property.TryGetDouble(out var seconds))
        {
            return Try
                .Run(() => TimeSpan.FromSeconds(seconds))
                .Match(
                    error => Invalid<TimeSpan>(error.Message),
                    Result<TimeSpan>.Success);
        }

        if (property.ValueKind == JsonValueKind.String &&
            TimeSpan.TryParse(
                property.GetString(),
                System.Globalization.CultureInfo.InvariantCulture,
                out var value))
        {
            return Result<TimeSpan>.Success(value);
        }

        return Invalid<TimeSpan>($"{propertyName} must be seconds or a TimeSpan string.");
    }

    private static Option<JsonElement> ReadProperty(
        JsonElement element,
        string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var property))
        {
            return Option<JsonElement>.Some(property);
        }

        return Option<JsonElement>.None();
    }

    private static Either<string, JsonElement> RequireProperty(
        JsonElement element,
        string propertyName)
        => ReadProperty(element, propertyName)
            .Match(
                () => Either<string, JsonElement>.FromLeft($"{propertyName} is required."),
                Either<string, JsonElement>.FromRight);

    private static Either<string, JsonElement> RequireValueKind(
        JsonElement element,
        JsonValueKind expected,
        string message)
        => element.ValueKind == expected
            ? Either<string, JsonElement>.FromRight(element)
            : Either<string, JsonElement>.FromLeft(message);

    private static Either<string, string> RequireNonEmpty(
        string? value,
        string message)
        => string.IsNullOrWhiteSpace(value)
            ? Either<string, string>.FromLeft(message)
            : Either<string, string>.FromRight(value);

    private static Either<string, int> ReadInt32(
        JsonElement element,
        string message)
        => element.TryGetInt32(out var value)
            ? Either<string, int>.FromRight(value)
            : Either<string, int>.FromLeft(message);

    private static Option<string> ReadNonEmptyValue(
        IReadOnlyDictionary<string, string> source,
        string key)
    {
        if (source.TryGetValue(key, out var value) &&
            !string.IsNullOrWhiteSpace(value))
        {
            return Option<string>.Some(value);
        }

        return Option<string>.None();
    }

    private static Result<T> Invalid<T>(
        string message)
        => Result<T>.Fail(LocalExecutionError(
            "LOCAL_EXECUTION_INVALID_PIPELINE",
            message));

    private static ErrorContext LocalExecutionError(
        string code,
        string message)
        => new(message, code, false)
        {
            FailureKind = FailureKind.Reject,
            OriginStep = OriginStep.KernelFacade,
            SemanticSlot = SemanticSlot.T
        };

    private static Result<string> Invalid(
        string message)
        => Invalid<string>(message);

    private static CapabilityInvocationResult Fail(
        CapabilityInvocationRequest request,
        IReadOnlyDictionary<string, string> metadata,
        string code,
        string message)
    {
        return new CapabilityInvocationResult(
            request.InvocationId,
            request.CapabilityId,
            Succeeded: false,
            OutputHash: null,
            ErrorCode: code,
            ErrorMessage: message,
            ReplayLogHash: request.ReplayLogHash,
            Metadata: metadata);
    }

    private static string? GetExecutionErrorCode(
        DslPipelineExecutionResult result)
        => RequireSucceeded(result)
            .Match(
                _ => result.Error?.Code ?? "LOCAL_EXECUTION_FAILED",
                _ => (string?)null);

    private static string? GetExecutionErrorMessage(
        DslPipelineExecutionResult result)
        => RequireSucceeded(result)
            .Match(
                left => result.Error?.Message ?? left,
                _ => (string?)null);

    private static Either<string, DslPipelineExecutionResult> RequireSucceeded(
        DslPipelineExecutionResult result)
    {
        if (result.Status == ExecutionStatus.Succeeded)
        {
            return Either<string, DslPipelineExecutionResult>.FromRight(result);
        }

        return Either<string, DslPipelineExecutionResult>.FromLeft(
            "Local DSL execution failed.");
    }

    private static string ComputeHash(
        DslPipelineExecutionResult result)
    {
        var payload = string.Join(
            "\n",
            result.Status,
            result.State.PipelineId,
            result.State.CurrentNode,
            result.State.ExecutedNodeCount,
            string.Join("\n", result.Output.Data
                .OrderBy(x => x.Key, StringComparer.Ordinal)
                .Select(x => $"{x.Key}={x.Value}")));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))
            .ToLowerInvariant();
    }
}

internal static class LocalExecutionEitherExtensions
{
    public static Result<KeyValuePair<string, string>> ToKeyValue(
        this string value,
        string key)
        => Result<KeyValuePair<string, string>>.Success(
            new KeyValuePair<string, string>(key, value));

    public static Result<T> MapError<T>(
        this Result<T> result,
        string code,
        FailureKind failureKind = FailureKind.FailClosed)
        => result.Match(
            error => Result<T>.Fail(error with
            {
                Code = code,
                FailureKind = failureKind,
                OriginStep = OriginStep.KernelFacade,
                SemanticSlot = SemanticSlot.T
            }),
            Result<T>.Success);

    public static Result<T> ToLocalExecutionResult<T>(
        this Either<string, T> value)
        => value.Match(
            left => Result<T>.Fail(new ErrorContext(
                left,
                "LOCAL_EXECUTION_INVALID_PIPELINE",
                false)
            {
                FailureKind = FailureKind.Reject,
                OriginStep = OriginStep.KernelFacade,
                SemanticSlot = SemanticSlot.T
            }),
            Result<T>.Success);

    public static Option<T> OrElseOption<T>(
        this Option<T> option,
        Option<T> fallback)
        => option.Match(
            () => fallback,
            Option<T>.Some);
}
