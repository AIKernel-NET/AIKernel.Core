namespace AIKernel.Core.Dsl;

using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using AIKernel.Common.Results;

internal static class DslDocumentParser
{
    /// <summary>
    /// EN: Executes Parse.
    /// EN: Documentation for public API. JA: Parse を実行します。
    /// </summary>
    public static Result<DslDocument> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Invalid<DslDocument>("DSL JSON is required.");
        }

        return Try
            .Run(() =>
            {
                using var document = JsonDocument.Parse(json);
                return ParseNode(document.RootElement)
                    .Bind(RequirePipelineRoot)
                    .Map(node => new DslDocument(node));
            })
            .Match(error => Invalid<DslDocument>(error.Message), result => result);
    }

    private static Result<PipelineNode> ParseNode(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return Invalid<PipelineNode>("Pipeline node must be a JSON object.");
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
            _ => Result<PipelineNode>.Fail(new ErrorContext(
                $"Unknown pipeline node type: {type}.",
                "INVALID_DSL",
                false)
            {
                FailureKind = FailureKind.Reject,
                OriginStep = OriginStep.KernelFacade,
                SemanticSlot = SemanticSlot.T
            })
        };
    }

    private static Result<PipelineNode> RequirePipelineRoot(PipelineNode node)
    {
        return node is PipelineRootNode
            ? Result<PipelineNode>.Success(node)
            : Invalid<PipelineNode>("DSL root node must be a Pipeline.");
    }

    private static Result<PipelineNode> ParsePipeline(JsonElement element)
    {
        return ParseNodeArray(element, "steps")
            .Map<PipelineNode>(steps => new PipelineRootNode(steps));
    }

    private static Result<PipelineNode> ParseStep(JsonElement element)
    {
        return ReadRequiredString(element, "name")
            .Map<PipelineNode>(name => new StepNode(name));
    }

    private static Result<PipelineNode> ParseCallCapability(JsonElement element)
    {
        return
            from name in ReadRequiredString(element, "name")
            from args in ReadArgs(element)
            select (PipelineNode)new CallCapabilityNode(name, args);
    }

    private static Result<PipelineNode> ParseLoop(JsonElement element)
    {
        return
            from maxIterations in ReadRequiredInt(element, "maxIterations")
            from body in ParseNodeArray(element, "body")
            select (PipelineNode)new LoopNode(maxIterations, body);
    }

    private static Result<PipelineNode> ParseLoopUntil(JsonElement element)
    {
        return
            from timeout in ReadRequiredTimeout(element, "timeout")
            from maxIterations in ReadRequiredInt(element, "maxIterations")
            from body in ParseNodeArray(element, "body")
            select (PipelineNode)new LoopUntilNode(timeout, maxIterations, body);
    }

    private static Result<PipelineNode> ParseSuspend(JsonElement element)
    {
        return ReadRequiredString(element, "reason")
            .Map<PipelineNode>(reason => new SuspendNode(reason));
    }

    private static Result<IReadOnlyList<PipelineNode>> ParseNodeArray(
        JsonElement element,
        string propertyName)
    {
        var array = from property in RequireProperty(element, propertyName)
                    from valid in RequireValueKind(
                        property,
                        JsonValueKind.Array,
                        $"{propertyName} must be a JSON array.")
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

    private static Result<string> ReadRequiredString(
        JsonElement element,
        string propertyName)
    {
        var value = from property in RequireProperty(element, propertyName)
                    from valid in RequireValueKind(
                        property,
                        JsonValueKind.String,
                        $"{propertyName} must be a string.")
                    from text in RequireNonEmpty(
                        valid.GetString(),
                        $"{propertyName} must not be empty.")
                    select text;

        return value.ToInvalidResult();
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

        return value.ToInvalidResult();
    }

    private static Result<TimeSpan> ReadRequiredTimeout(
        JsonElement element,
        string propertyName)
    {
        return ReadProperty(element, propertyName)
            .Match(
                () => Invalid<TimeSpan>($"{propertyName} is required."),
                property => ReadTimeoutValue(property, propertyName));
    }

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
                    _ => Invalid<TimeSpan>(
                        $"{propertyName} must be a finite TimeSpan value within the TimeSpan range."),
                    Result<TimeSpan>.Success);
        }

        if (property.ValueKind == JsonValueKind.String &&
            TimeSpan.TryParse(
                property.GetString(),
                CultureInfo.InvariantCulture,
                out var timeout))
        {
            return Result<TimeSpan>.Success(timeout);
        }

        return Result<TimeSpan>.Fail(new ErrorContext(
            $"{propertyName} must be a number of seconds or a TimeSpan string.",
            "INVALID_DSL",
            false)
        {
            FailureKind = FailureKind.Reject,
            OriginStep = OriginStep.KernelFacade,
            SemanticSlot = SemanticSlot.T
        });
    }

    private static Result<IReadOnlyDictionary<string, string>> ReadArgs(JsonElement element)
        => ReadProperty(element, "args")
            .Match(
                () => Result<IReadOnlyDictionary<string, string>>.Success(
                    ImmutableDictionary<string, string>.Empty),
                ReadArgsObject);

    private static Result<IReadOnlyDictionary<string, string>> ReadArgsObject(
        JsonElement args)
    {
        if (args.ValueKind != JsonValueKind.Object)
        {
            return Invalid<IReadOnlyDictionary<string, string>>(
                "args must be a JSON object.");
        }

        return args
            .EnumerateObject()
            .Aggregate(
                Result<ImmutableDictionary<string, string>>.Success(
                    ImmutableDictionary<string, string>.Empty),
                (current, item) =>
                    from values in current
                    from parsed in ReadArg(item)
                    select values.SetItem(parsed.Key, parsed.Value))
            .Map<IReadOnlyDictionary<string, string>>(values => values);
    }

    private static Result<KeyValuePair<string, string>> ReadArg(
        JsonProperty item)
    {
        var arg = from key in RequireNonEmpty(
                      item.Name,
                      "args keys must not be empty.")
                  from value in SelectJsonArgumentValue(item.Value)
                  select new KeyValuePair<string, string>(key, value);

        return arg.ToInvalidResult();
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

    private static Either<string, string> SelectJsonArgumentValue(
        JsonElement value)
        => value.ValueKind == JsonValueKind.String
            ? Either<string, string>.FromRight(value.GetString() ?? string.Empty)
            : Either<string, string>.FromRight(value.GetRawText());

    private static Result<T> Invalid<T>(string message)
        => Result<T>.Fail(new ErrorContext(message, "INVALID_DSL", false)
        {
            FailureKind = FailureKind.Reject,
            OriginStep = OriginStep.KernelFacade,
            SemanticSlot = SemanticSlot.T
        });

    private static Result<string> Invalid(string message)
        => Invalid<string>(message);
}

internal static class DslDocumentParserEitherExtensions
{
    /// <summary>
    /// EN: Gets ToInvalidResult&lt;T&gt;.
    /// EN: Documentation for public API. JA: ToInvalidResult&lt;T&gt; を取得します。
    /// </summary>
    public static Result<T> ToInvalidResult<T>(
        this Either<string, T> value)
        => value.Match(
            left => Result<T>.Fail(new ErrorContext(left, "INVALID_DSL", false)
            {
                FailureKind = FailureKind.Reject,
                OriginStep = OriginStep.KernelFacade,
                SemanticSlot = SemanticSlot.T
            }),
            Result<T>.Success);
}
