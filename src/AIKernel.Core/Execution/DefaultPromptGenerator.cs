namespace AIKernel.Core.Execution;

using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AIKernel.Abstractions.Execution;
using AIKernel.Abstractions.Providers;
using AIKernel.Common.Results;
using AIKernel.Dtos.Execution;
using AIKernel.Enums;
using ExecutionModelMessage = AIKernel.Dtos.Execution.ModelMessage;

public sealed class DefaultPromptGenerator : IPromptGenerator
{
    private const string SystemRole = "system";
    private const string UserRole = "user";

    private readonly IContextPromptProjector _projector;
    private readonly ITokenizer _tokenizer;

    public DefaultPromptGenerator(
        IContextPromptProjector projector,
        ITokenizer tokenizer)
    {
        _projector = projector ?? throw new ArgumentNullException(nameof(projector));
        _tokenizer = tokenizer ?? throw new ArgumentNullException(nameof(tokenizer));
    }

    public Task<GeneratedPrompt> GenerateAsync(
        PromptGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.ContextBlocks);
        ArgumentNullException.ThrowIfNull(request.Capability);

        cancellationToken.ThrowIfCancellationRequested();

        ValidateCapability(request.Capability);

        var blocks = NormalizeBlocks(request.ContextBlocks, request.Options.IncludeSourceMetadata);

        var messages = BuildMessages(request, blocks);
        var estimatedTokens = CountMessages(messages);

        if (estimatedTokens > request.Capability.MaxInputTokens)
        {
            if (request.Options.OverflowPolicy == PromptOverflowPolicy.FailClosed)
            {
                throw new PromptTokenBudgetExceededException(
                    estimatedTokens,
                    request.Capability.MaxInputTokens);
            }

            messages = TrimByPriority(
                request,
                blocks,
                request.Capability.MaxInputTokens,
                cancellationToken);

            estimatedTokens = CountMessages(messages);
        }

        if (estimatedTokens > request.Capability.MaxInputTokens)
        {
            throw new PromptTokenBudgetExceededException(
                estimatedTokens,
                request.Capability.MaxInputTokens);
        }

        var promptHash = ComputePromptHash(
            request.ContextHash,
            request.Capability,
            messages);

        var generated = new GeneratedPrompt
        {
            PromptId = $"prompt:{promptHash}",
            PromptHash = promptHash,
            ContextSnapshotId = request.ContextSnapshotId,
            ContextHash = request.ContextHash,
            Capability = request.Capability,
            Messages = messages.ToImmutableArray(),
            EstimatedInputTokens = estimatedTokens,
            Metadata = ImmutableDictionary<string, string>.Empty
                .Add(ExecutionMetadataKeys.MessageFormat, request.Capability.MessageFormat.ToString())
                .Add(ExecutionMetadataKeys.OverflowPolicy, request.Options.OverflowPolicy.ToString())
        };

        return Task.FromResult(generated);
    }

    private static void ValidateCapability(ModelPromptCapability capability)
    {
        if (string.IsNullOrWhiteSpace(capability.ProviderId))
        {
            throw new UnsupportedPromptCapabilityException("ProviderId is required.");
        }

        if (string.IsNullOrWhiteSpace(capability.ModelId))
        {
            throw new UnsupportedPromptCapabilityException("ModelId is required.");
        }

        if (capability.MaxInputTokens <= 0)
        {
            throw new UnsupportedPromptCapabilityException("MaxInputTokens must be greater than zero.");
        }

        if (capability.MaxOutputTokens <= 0)
        {
            throw new UnsupportedPromptCapabilityException("MaxOutputTokens must be greater than zero.");
        }

        if (capability.SupportedRoles.Length == 0)
        {
            throw new UnsupportedPromptCapabilityException("SupportedRoles must not be empty.");
        }

        if (!capability.SupportedRoles.Contains(UserRole))
        {
            throw new UnsupportedPromptCapabilityException("Capability must support the user role.");
        }
    }

    private static IReadOnlyList<ExecutionModelMessage> BuildMessages(
        PromptGenerationRequest request,
        IReadOnlyList<ContextPromptBlock> blocks)
    {
        return request.Capability.MessageFormat switch
        {
            PromptMessageFormat.ChatMessages => BuildChatMessages(request, blocks),
            PromptMessageFormat.AlternatingMessages => BuildSingleUserMessage(request, blocks),
            PromptMessageFormat.SingleTextPrompt => BuildSingleUserMessage(request, blocks),
            _ => throw new UnsupportedPromptCapabilityException(
                $"Unsupported prompt message format: {request.Capability.MessageFormat}")
        };
    }

    private static IReadOnlyList<ExecutionModelMessage> BuildChatMessages(
        PromptGenerationRequest request,
        IReadOnlyList<ContextPromptBlock> blocks)
    {
        var systemText = BuildSystemInstruction(request, blocks);
        var systemRole = request.Capability.SupportsSystemMessages
            ? SystemRole
            : request.Capability.SystemInstructionRole;

        return
        [
            new ExecutionModelMessage(systemRole, systemText),
            new ExecutionModelMessage(UserRole, request.UserInstruction.Trim())
        ];
    }

    private static IReadOnlyList<ExecutionModelMessage> BuildSingleUserMessage(
        PromptGenerationRequest request,
        IReadOnlyList<ContextPromptBlock> blocks)
    {
        var text = BuildSystemInstruction(request, blocks)
            + "\n\nUser instruction:\n"
            + request.UserInstruction.Trim();

        return [new ExecutionModelMessage(UserRole, text)];
    }

    private static string BuildSystemInstruction(
        PromptGenerationRequest request,
        IReadOnlyList<ContextPromptBlock> blocks)
    {
        var builder = new StringBuilder();

        builder.AppendLine("You are executing inside AIKernel.NET.");
        builder.AppendLine("Use only the supplied ContextSnapshot.");
        builder.AppendLine("Do not infer hidden context.");
        builder.AppendLine();

        if (request.Options.IncludeContextHash)
        {
            builder.AppendLine($"ContextSnapshotId: {request.ContextSnapshotId}");
            builder.AppendLine($"ContextHash: {request.ContextHash}");
            builder.AppendLine();
        }

        builder.AppendLine("Context:");

        foreach (var block in blocks
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.Id, StringComparer.Ordinal))
        {
            builder.AppendLine($"--- block: {block.Id} category: {block.Category} ---");
            builder.AppendLine(block.Content.Trim());
            builder.AppendLine();
        }

        return builder.ToString().Trim();
    }

    private IReadOnlyList<ExecutionModelMessage> TrimByPriority(
        PromptGenerationRequest request,
        IReadOnlyList<ContextPromptBlock> blocks,
        int maxInputTokens,
        CancellationToken cancellationToken)
    {
        var selected = blocks
            .OrderByDescending(x => x.Priority)
            .ThenBy(x => x.Id, StringComparer.Ordinal)
            .ToList();

        while (selected.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var messages = BuildMessages(request, selected);
            var count = CountMessages(messages);

            if (count <= maxInputTokens)
            {
                return messages;
            }

            selected.RemoveAt(selected.Count - 1);
        }

        var minimal = BuildMessages(request, []);

        if (CountMessages(minimal) <= maxInputTokens)
        {
            return minimal;
        }

        throw new PromptTokenBudgetExceededException(
            CountMessages(minimal),
            maxInputTokens);
    }

    private int CountMessages(IReadOnlyList<ExecutionModelMessage> messages)
    {
        var total = 0;

        foreach (var message in messages)
        {
            total += _tokenizer.CountTokens(message.Role);
            total += _tokenizer.CountTokens(message.Content);
        }

        return total;
    }

    private static string ComputePromptHash(
        string contextHash,
        ModelPromptCapability capability,
        IReadOnlyList<ExecutionModelMessage> messages)
    {
        var payload = new
        {
            context_hash = contextHash,
            capability = new
            {
                provider_id = capability.ProviderId,
                model_id = capability.ModelId,
                message_format = capability.MessageFormat.ToString(),
                max_input_tokens = capability.MaxInputTokens,
                max_output_tokens = capability.MaxOutputTokens,
                supported_roles = capability.SupportedRoles
                    .OrderBy(x => x, StringComparer.Ordinal)
                    .ToArray()
            },
            messages = messages.Select(x => new
            {
                role = x.Role,
                content = x.Content
            }).ToArray()
        };

        var json = JsonSerializer.Serialize(payload);
        var bytes = Encoding.UTF8.GetBytes(json);
        var hash = SHA256.HashData(bytes);

        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static IReadOnlyList<ContextPromptBlock> NormalizeBlocks(
        IReadOnlyList<ContextPromptBlock> blocks,
        bool includeSourceMetadata)
    {
        if (includeSourceMetadata)
        {
            return blocks;
        }

        return blocks
            .Select(block => block with
            {
                Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            })
            .ToArray();
    }
}
