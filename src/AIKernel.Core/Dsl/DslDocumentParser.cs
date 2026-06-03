namespace AIKernel.Core.Dsl;

using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using AIKernel.Common.Results;

public static class DslDocumentParser
{
    public static Result<DslDocument> Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return Invalid<DslDocument>("DSL JSON is required.");
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return ParseNode(document.RootElement)
                .Map(node => new DslDocument(node));
        }
        catch (JsonException ex)
        {
            return Invalid<DslDocument>(ex.Message);
        }
    }

    private static Result<PipelineNode> ParseNode(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return Invalid<PipelineNode>("Pipeline node must be a JSON object.");
        }

        var type = ReadRequiredString(element, "type");
        if (type.IsFailure)
            return Result<PipelineNode>.Fail(type.Error!);

        return type.Value switch
        {
            "Pipeline" => ParsePipeline(element),
            "Step" => ParseStep(element),
            "CallCapability" => ParseCallCapability(element),
            "Loop" => ParseLoop(element),
            "LoopUntil" => ParseLoopUntil(element),
            "Suspend" => ParseSuspend(element),
            _ => Result<PipelineNode>.Fail(new ErrorContext(
                $"Unknown pipeline node type: {type.Value}.",
                "INVALID_DSL",
                false)
            {
                FailureKind = FailureKind.Reject,
                OriginStep = OriginStep.KernelFacade,
                SemanticSlot = SemanticSlot.T
            })
        };
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
        return ReadRequiredString(element, "name")
            .Map<PipelineNode>(name => new CallCapabilityNode(
                name,
                ReadArgs(element)));
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
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Array)
        {
            return Result<IReadOnlyList<PipelineNode>>.Fail(new ErrorContext(
                $"{propertyName} must be a JSON array.",
                "INVALID_DSL",
                false)
            {
                FailureKind = FailureKind.Reject,
                OriginStep = OriginStep.KernelFacade,
                SemanticSlot = SemanticSlot.T
            });
        }

        var nodes = new List<PipelineNode>();
        foreach (var child in property.EnumerateArray())
        {
            var node = ParseNode(child);
            if (node.IsFailure)
                return Result<IReadOnlyList<PipelineNode>>.Fail(node.Error!);

            nodes.Add(node.Value!);
        }

        return Result<IReadOnlyList<PipelineNode>>.Success(nodes.ToArray());
    }

    private static Result<string> ReadRequiredString(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return Invalid($"{propertyName} must be a string.");
        }

        var value = property.GetString();
        return string.IsNullOrWhiteSpace(value)
            ? Invalid($"{propertyName} must not be empty.")
            : Result<string>.Success(value);
    }

    private static Result<int> ReadRequiredInt(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.Number ||
            !property.TryGetInt32(out var value))
        {
            return Result<int>.Fail(new ErrorContext(
                $"{propertyName} must be an integer.",
                "INVALID_DSL",
                false)
            {
                FailureKind = FailureKind.Reject,
                OriginStep = OriginStep.KernelFacade,
                SemanticSlot = SemanticSlot.T
            });
        }

        return Result<int>.Success(value);
    }

    private static Result<TimeSpan> ReadRequiredTimeout(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return Result<TimeSpan>.Fail(new ErrorContext(
                $"{propertyName} is required.",
                "INVALID_DSL",
                false)
            {
                FailureKind = FailureKind.Reject,
                OriginStep = OriginStep.KernelFacade,
                SemanticSlot = SemanticSlot.T
            });
        }

        if (property.ValueKind == JsonValueKind.Number &&
            property.TryGetDouble(out var seconds))
        {
            return Result<TimeSpan>.Success(TimeSpan.FromSeconds(seconds));
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

    private static IReadOnlyDictionary<string, string> ReadArgs(JsonElement element)
    {
        if (!element.TryGetProperty("args", out var args) ||
            args.ValueKind != JsonValueKind.Object)
        {
            return ImmutableDictionary<string, string>.Empty;
        }

        var builder = ImmutableDictionary.CreateBuilder<string, string>(
            StringComparer.Ordinal);

        foreach (var item in args.EnumerateObject())
        {
            builder[item.Name] = item.Value.ValueKind == JsonValueKind.String
                ? item.Value.GetString() ?? string.Empty
                : item.Value.GetRawText();
        }

        return builder.ToImmutable();
    }

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
