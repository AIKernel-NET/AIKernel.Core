using System.Text.Encodings.Web;
using System.Text.Json;

namespace AIKernel.Kernel.Inference;

// Prompt data is JSON, not HTML. Preserve punctuation while preventing raw chat control tokens.
// This is a representation experiment, not a fact normalizer or a replacement governance rule.
internal static class BonsaiCtgReadableJson
{
    internal const string Contract="canonical-readable-assessment-json-v8";
    private static readonly JsonSerializerOptions Options=new(){Encoder=JavaScriptEncoder.UnsafeRelaxedJsonEscaping};
    internal static string Serialize<T>(T value)=>JsonSerializer.Serialize(value,Options)
        .Replace("<","\\u003C",StringComparison.Ordinal).Replace(">","\\u003E",StringComparison.Ordinal);
}
