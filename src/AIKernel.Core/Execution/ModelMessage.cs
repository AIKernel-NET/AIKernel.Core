namespace AIKernel.Core.Execution;

using AIKernel.Abstractions.Providers;

/// <summary>[EN] Documents this public package API member. [JA] ModelMessage を表します。</summary>
/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.ModelMessage']/summary" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.ModelMessage']/summary" />
public sealed record ModelMessage(
    string Role,
    string Content) : IModelMessage;
