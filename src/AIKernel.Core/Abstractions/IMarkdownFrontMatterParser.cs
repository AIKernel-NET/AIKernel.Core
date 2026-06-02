#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Abstractions.Rom;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

using AIKernel.Dtos.Rom;

public interface IMarkdownFrontMatterParser
{
    MarkdownFrontMatterDocument Parse(
        string markdown,
        string sourcePath);
}