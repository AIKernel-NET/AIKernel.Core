namespace AIKernel.Core.ChatHistory;

using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using AIKernel.Common.Results;
using AIKernel.Core.Rom;
using AIKernel.Dtos.Rom;

internal sealed record ChatHistoryRomRecord(
    string Role,
    string Content,
    DateTimeOffset Timestamp);

internal sealed record ChatHistoryRomOptions(
    string RomId,
    DateTimeOffset GeneratedAtUtc,
    string EntityType = "conversation",
    string Version = "1",
    IReadOnlyList<string>? SecurityTags = null);

internal sealed class ChatHistoryRomExporter :
    AIKernel.Abstractions.History.IChatHistoryRomExporter
{
    private const string SourceKind = "chat_history";
    /// <summary>
    /// EN: Gets ToRomMarkdown.
    /// [EN] Documents this public package API member. [JA] ToRomMarkdown を取得します。
    /// </summary>

    public static Result<string> ToRomMarkdown(
        IReadOnlyList<ChatHistoryRomRecord> records,
        ChatHistoryRomOptions options)
        => from _ in Validate(records, options)
           from markdown in BuildRomMarkdownResult(records, options)
           select markdown;

    private static Result<string> BuildRomMarkdownResult(
        IReadOnlyList<ChatHistoryRomRecord> records,
        ChatHistoryRomOptions options)
    {
        var normalizedRecords = records
            .Select((record, index) => new NormalizedRecord(
                index + 1,
                record.Role.Trim(),
                NormalizeContent(record.Content),
                record.Timestamp.ToUniversalTime()))
            .ToArray();

        var body = BuildBody(normalizedRecords);
        var metadata = BuildMetadata(options);
        var securityTags = NormalizeSecurityTags(options.SecurityTags);

        var candidate = new RomSnapshotCandidate
        {
            RomId = RomIdFactory.Create(options.RomId, nameof(options.RomId)),
            SourcePath = $"rom/chat-history/{SanitizeRomId(options.RomId)}.md",
            Body = body,
            SecurityTags = securityTags,
            Relations = ImmutableArray<RomRelationSnapshot>.Empty,
            ExpectedHash = string.Empty,
            AdditionalMetadata = metadata
        };

        var hash = new Sha256SemanticHasher()
            .ComputeHash(new DefaultRomCanonicalizer().Canonicalize(
                new ChatHistoryRomDocument(candidate)));

        return Result<string>.Success(BuildMarkdown(
            options,
            securityTags,
            hash,
            body));
    }

    Task<string> AIKernel.Abstractions.History.IChatHistoryRomExporter.ToRomMarkdownAsync(
        IReadOnlyList<AIKernel.Dtos.History.ChatHistoryRomRecord> records,
        AIKernel.Dtos.History.ChatHistoryRomOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = ToRomMarkdown(
            HistoryRomContractMapper.ToCore(records),
            HistoryRomContractMapper.ToCore(options));

        return result.Match(
            error => throw new InvalidOperationException(error.Message),
            Task.FromResult);
    }

    private static Result<bool> Validate(
        IReadOnlyList<ChatHistoryRomRecord>? records,
        ChatHistoryRomOptions? options)
    {
        if (options is null)
        {
            return Fail("Chat history ROM options are required.");
        }

        if (string.IsNullOrWhiteSpace(options.RomId))
        {
            return Fail("Chat history ROM id is required.");
        }

        if (string.IsNullOrWhiteSpace(options.EntityType))
        {
            return Fail("Chat history ROM entity type is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Version))
        {
            return Fail("Chat history ROM version is required.");
        }

        if (records is null || records.Count == 0)
        {
            return Fail("Chat history records are required.");
        }

        for (var i = 0; i < records.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(records[i].Role))
            {
                return Fail($"Chat history record role is required. Index='{i}'.");
            }

            if (records[i].Content is null)
            {
                return Fail($"Chat history record content is required. Index='{i}'.");
            }
        }

        return Result<bool>.Success(true);
    }

    private static ImmutableDictionary<string, string> BuildMetadata(
        ChatHistoryRomOptions options)
        => ImmutableDictionary<string, string>.Empty
            .Add("entity_type", options.EntityType.Trim())
            .Add("version", options.Version.Trim())
            .Add("source_kind", SourceKind)
            .Add(
                "generated_at",
                options.GeneratedAtUtc.ToUniversalTime()
                    .ToString("O", CultureInfo.InvariantCulture));

    private static ImmutableArray<string> NormalizeSecurityTags(
        IReadOnlyList<string>? tags)
        => (tags ?? ["history", "chat"])
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .ToImmutableArray();

    private static string BuildBody(IReadOnlyList<NormalizedRecord> records)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# ROM:ChatHistory");
        builder.AppendLine();

        foreach (var record in records)
        {
            builder.AppendLine($"## Turn:{record.Turn}");
            builder.AppendLine($"@role: {record.Role}");
            builder.AppendLine(
                $"@time: {record.Timestamp.ToString("O", CultureInfo.InvariantCulture)}");
            builder.AppendLine();
            builder.AppendLine(record.Content);
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string BuildMarkdown(
        ChatHistoryRomOptions options,
        ImmutableArray<string> securityTags,
        string hash,
        string body)
    {
        var builder = new StringBuilder();
        builder.AppendLine("---");
        builder.AppendLine($"rom_id: {QuoteYaml(options.RomId.Trim())}");
        builder.AppendLine($"entity_type: {QuoteYaml(options.EntityType.Trim())}");
        builder.AppendLine($"version: {QuoteYaml(options.Version.Trim())}");
        builder.AppendLine($"source_kind: {QuoteYaml(SourceKind)}");
        builder.AppendLine(
            "generated_at: " +
            QuoteYaml(options.GeneratedAtUtc.ToUniversalTime()
                .ToString("O", CultureInfo.InvariantCulture)));
        builder.AppendLine("security:");
        builder.AppendLine("  tags:");

        foreach (var tag in securityTags)
        {
            builder.AppendLine($"    - {QuoteYaml(tag)}");
        }

        builder.AppendLine("signature:");
        builder.AppendLine($"  hash: {QuoteYaml(hash)}");
        builder.AppendLine("---");
        builder.Append(body);
        return builder.ToString();
    }

    private static string NormalizeContent(string content)
        => content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\r", "\n", StringComparison.Ordinal)
            .Trim();

    private static string QuoteYaml(string value)
        => "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";

    private static string SanitizeRomId(string romId)
    {
        var builder = new StringBuilder(romId.Length);
        foreach (var c in romId)
        {
            builder.Append(char.IsLetterOrDigit(c) || c is '-' or '_' or '.'
                ? c
                : '-');
        }

        return builder.ToString().Trim('-');
    }

    private static Result<bool> Fail(string message)
        => Result<bool>.Fail(new ErrorContext(
            message,
            "CHAT_HISTORY_ROM_ERROR",
            false)
        {
            FailureKind = FailureKind.FailClosed,
            OriginStep = OriginStep.KernelFacade,
            SemanticSlot = SemanticSlot.C
        });

    private sealed record NormalizedRecord(
        int Turn,
        string Role,
        string Content,
        DateTimeOffset Timestamp);

    private sealed class ChatHistoryRomDocument(
        RomSnapshotCandidate candidate) : AIKernel.Abstractions.Rom.IRomDocument
    {
        /// <summary>
        /// EN: Gets EntityId.
        /// [EN] Documents this public package API member. [JA] EntityId を取得します。
        /// </summary>
        public string EntityId => candidate.RomId.Value;
        /// <summary>
        /// EN: Gets EntityType.
        /// [EN] Documents this public package API member. [JA] EntityType を取得します。
        /// </summary>

        public string EntityType =>
            candidate.AdditionalMetadata.TryGetValue("entity_type", out var entityType)
                ? entityType
                : "conversation";
        /// <summary>
        /// EN: Gets Version.
        /// [EN] Documents this public package API member. [JA] Version を取得します。
        /// </summary>

        public string Version =>
            candidate.AdditionalMetadata.TryGetValue("version", out var version)
                ? version
                : "1";
        /// <summary>
        /// EN: Gets Body.
        /// [EN] Documents this public package API member. [JA] Body を取得します。
        /// </summary>

        public string Body => candidate.Body;
        /// <summary>
        /// EN: Gets Metadata.
        /// [EN] Documents this public package API member. [JA] Metadata を取得します。
        /// </summary>

        public IReadOnlyDictionary<string, string> Metadata => candidate.AdditionalMetadata;
        /// <summary>
        /// EN: Gets RelationReferences.
        /// [EN] Documents this public package API member. [JA] RelationReferences を取得します。
        /// </summary>

        public IReadOnlyList<string> RelationReferences => [];
        /// <summary>
        /// EN: Executes GetSemanticHashAsync.
        /// [EN] Documents this public package API member. [JA] GetSemanticHashAsync を実行します。
        /// </summary>

        public Task<string> GetSemanticHashAsync()
            => Task.FromResult(new Sha256SemanticHasher()
                .ComputeHash(new DefaultRomCanonicalizer().Canonicalize(this)));
        /// <summary>
        /// EN: Executes CanonicalizeAsync.
        /// [EN] Documents this public package API member. [JA] CanonicalizeAsync を実行します。
        /// </summary>

        public Task<CanonicalizedRomDto> CanonicalizeAsync()
            => new DefaultRomCanonicalizer().CanonicalizeAsync(this);
    }
}
