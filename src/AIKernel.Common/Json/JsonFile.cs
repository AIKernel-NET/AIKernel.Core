namespace AIKernel.Common.Json;

/// <summary>EN: Documentation for public API. JA: JsonFile を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Json.JsonFile']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Json.JsonFile']/summary" />
public static class JsonFile
{
    /// <summary>EN: Documentation for public API. JA: LoadAsync&lt;T&gt; を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Json.JsonFile.LoadAsync&lt;T&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Json.JsonFile.LoadAsync&lt;T&gt;']/summary" />
    public static async Task<T?> LoadAsync<T>(string path)
    {
        var json = await File.ReadAllTextAsync(path);
        return JsonUtil.FromJson<T>(json);
    }

    /// <summary>EN: Documentation for public API. JA: SaveAsync&lt;T&gt; を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Json.JsonFile.SaveAsync&lt;T&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Json.JsonFile.SaveAsync&lt;T&gt;']/summary" />
    public static async Task SaveAsync<T>(string path, T value, bool indented = true)
    {
        var json = JsonUtil.ToJson(value, indented);
        await File.WriteAllTextAsync(path, json);
    }
}
