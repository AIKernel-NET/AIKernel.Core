using System.Text.Json;

namespace AIKernel.Common.Json;

/// <summary>[EN] Documents this public package API member. [JA] JsonUtil を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Json.JsonUtil']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Json.JsonUtil']/summary" />
public static class JsonUtil
{
    /// <summary>[EN] Documents this public package API member. [JA] ToJson&lt;T&gt; を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Json.JsonUtil.ToJson&lt;T&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Json.JsonUtil.ToJson&lt;T&gt;']/summary" />
    public static string ToJson<T>(T value, bool indented = false)
        => JsonSerializer.Serialize(value, indented ? JsonOptions.Indented : JsonOptions.Default);

    /// <summary>[EN] Documents this public package API member. [JA] FromJson&lt;T&gt; を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Json.JsonUtil.FromJson&lt;T&gt;']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Json.JsonUtil.FromJson&lt;T&gt;']/summary" />
    public static T? FromJson<T>(string json)
        => JsonSerializer.Deserialize<T>(json, JsonOptions.Default);

    /// <summary>[EN] Documents this public package API member. [JA] FromJson を実行します。</summary>
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Json.JsonUtil.FromJson']/summary" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Json.JsonUtil.FromJson']/summary" />
    public static object? FromJson(string json, Type type)
        => JsonSerializer.Deserialize(json, type, JsonOptions.Default);
}
