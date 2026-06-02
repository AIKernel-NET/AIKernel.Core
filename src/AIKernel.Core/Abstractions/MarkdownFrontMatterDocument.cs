#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Dtos.Rom;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

using System.Collections.Immutable;

public sealed record MarkdownFrontMatterDocument(
    string SourcePath,
    string Body,
    ImmutableDictionary<string, object?> FrontMatter);