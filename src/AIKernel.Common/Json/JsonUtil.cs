using System.Text.Json;

namespace AIKernel.Common.Json;

public static class JsonUtil
{
    public static string ToJson<T>(T value, bool indented = false)
        => JsonSerializer.Serialize(value, indented ? JsonOptions.Indented : JsonOptions.Default);

    public static T? FromJson<T>(string json)
        => JsonSerializer.Deserialize<T>(json, JsonOptions.Default);

    public static object? FromJson(string json, Type type)
        => JsonSerializer.Deserialize(json, type, JsonOptions.Default);
}
