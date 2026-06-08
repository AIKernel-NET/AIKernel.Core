using System.Text.Encodings.Web;
using System.Text.Json;

namespace AIKernel.Common.Json;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Common.Json.JsonOptions']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Common.Json.JsonOptions']" />
public static class JsonOptions
{
    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Json.JsonOptions.new']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Json.JsonOptions.new']" />
    public static readonly JsonSerializerOptions Default = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <include file="docs.en.xml" path="doc/members/member[@name='M:AIKernel.Common.Json.JsonOptions.new']" />
    /// <include file="docs.ja.xml" path="doc/members/member[@name='M:AIKernel.Common.Json.JsonOptions.new']" />
    public static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}
