namespace AIKernel.Common.Json;

public static class JsonFile
{
    public static async Task<T?> LoadAsync<T>(string path)
    {
        var json = await File.ReadAllTextAsync(path);
        return JsonUtil.FromJson<T>(json);
    }

    public static async Task SaveAsync<T>(string path, T value, bool indented = true)
    {
        var json = JsonUtil.ToJson(value, indented);
        await File.WriteAllTextAsync(path, json);
    }
}
