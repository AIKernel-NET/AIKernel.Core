using System.Text.Json;

namespace AIKernel.Common.Json;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Json.JsonUtil']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Json.JsonUtil']" />
public static class JsonUtil
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Json.JsonUtil.ToJson&lt;T&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Json.JsonUtil.ToJson&lt;T&gt;']" />
    public static string ToJson<T>(T value, bool indented = false)
        => JsonSerializer.Serialize(value, indented ? JsonOptions.Indented : JsonOptions.Default);

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Json.JsonUtil.FromJson&lt;T&gt;']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Json.JsonUtil.FromJson&lt;T&gt;']" />
    public static T? FromJson<T>(string json)
        => JsonSerializer.Deserialize<T>(json, JsonOptions.Default);

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Json.JsonUtil.FromJson']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Json.JsonUtil.FromJson']" />
    public static object? FromJson(string json, Type type)
        => JsonSerializer.Deserialize(json, type, JsonOptions.Default);
}
