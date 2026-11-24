using System.Text.Json;
using System.Text.Json.Nodes;

namespace AIKernel.Kernel.Inference;

/// <summary>A catalog is self-contained. Profile IDs never name files or remote resources.</summary>
internal static class DenseProfileResolver
{
    internal static string Resolve(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowDuplicateProperties = false });
        var root = document.RootElement;
        RequireObject(root);
        if (!root.TryGetProperty("profiles", out var profiles)) return json;
        if (root.GetPropertyCount() != 2 || !root.TryGetProperty("profile", out var selected))
            throw new InvalidDataException("A catalog requires exactly profile and profiles.");
        return ResolveCatalog(Name(selected), ReadCatalog(profiles));
    }

    private static Dictionary<string, JsonElement> ReadCatalog(JsonElement profiles)
    {
        RequireObject(profiles);
        var catalog = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var entry in profiles.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(entry.Name)) throw new InvalidDataException("Empty profile ID.");
            ValidateLayer(entry.Value);
            catalog.Add(entry.Name, entry.Value);
        }
        ValidateChains(catalog);
        return catalog;
    }

    private static string ResolveCatalog(string selected, Dictionary<string, JsonElement> catalog)
    {
        var resolved = new JsonObject();
        foreach (var layer in Chain(selected, catalog)) Merge(resolved, layer);
        return resolved.ToJsonString();
    }

    private static void ValidateChains(Dictionary<string, JsonElement> catalog)
    {
        var resolved = new HashSet<string>(StringComparer.Ordinal);
        foreach (string id in catalog.Keys)
        {
            if (!resolved.Contains(id)) ValidateAncestors(id, catalog, resolved);
        }
    }

    private static void ValidateAncestors(string id, Dictionary<string, JsonElement> catalog, HashSet<string> resolved)
    {
        var pending = new HashSet<string>(StringComparer.Ordinal);
        while (!resolved.Contains(id))
        {
            if (!pending.Add(id)) throw new InvalidDataException("Cycle in model profile inheritance: " + id);
            if (!catalog.TryGetValue(id, out var layer)) throw new InvalidDataException("Unknown model profile: " + id);
            if (!layer.TryGetProperty("extends", out var parent)) break;
            id = Name(parent);
        }
        resolved.UnionWith(pending);
    }

    private static Stack<JsonElement> Chain(string id, Dictionary<string, JsonElement> catalog)
    {
        var result = new Stack<JsonElement>();
        while (true)
        {
            if (!catalog.TryGetValue(id, out var layer)) throw new InvalidDataException("Unknown model profile: " + id);
            result.Push(layer);
            if (!layer.TryGetProperty("extends", out var parent)) return result;
            id = Name(parent);
        }
    }

    private static string Name(JsonElement value) =>
        value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()! : throw new InvalidDataException("Expected a nonempty profile name.");

    private static void RequireObject(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Expected a profile object.");
    }

    private static void ValidateLayer(JsonElement layer)
    {
        RequireObject(layer);
        foreach (var property in layer.EnumerateObject())
        {
            if (property.Name == "shape") ValidateShape(property.Value);
            else if (property.Name is "extends" or "modelId" or "version" or "readerId" or "tokenizerVersion" or "weightsSha256" or "tokenizerSha256")
                _ = Name(property.Value);
            else throw new InvalidDataException("Unknown model profile field: " + property.Name);
        }
    }

    private static void ValidateShape(JsonElement shape)
    {
        RequireObject(shape);
        foreach (var p in shape.EnumerateObject())
        {
            if (p.Name is not ("width" or "hidden" or "heads" or "kvHeads" or "headSize" or "layers" or "epsilon"))
                throw new InvalidDataException("Unknown shape field: " + p.Name);
            if (!PositiveDimension(p)) throw new InvalidDataException("Expected a positive dimension: " + p.Name);
        }
    }

    private static bool PositiveDimension(JsonProperty property)
    {
        if (property.Value.ValueKind != JsonValueKind.Number) return false;
        if (property.Name == "epsilon")
            return property.Value.TryGetSingle(out float epsilon) && float.IsFinite(epsilon) && epsilon > 0;
        return property.Value.TryGetInt32(out int size) && size > 0;
    }

    private static void Merge(JsonObject result, JsonElement layer)
    {
        foreach (var p in layer.EnumerateObject())
        {
            if (p.Name == "extends") continue;
            if (p.Name == "shape")
            {
                if (result["shape"] is not JsonObject shape) result["shape"] = shape = new JsonObject();
                Merge(shape, p.Value);
            }
            else result[p.Name] = JsonNode.Parse(p.Value.GetRawText());
        }
    }
}
