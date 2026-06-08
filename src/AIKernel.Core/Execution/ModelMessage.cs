namespace AIKernel.Core.Execution;

using AIKernel.Abstractions.Providers;

/// <include file="docs.en.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.ModelMessage']" />
/// <include file="docs.ja.xml" path="doc/members/member[@name='T:AIKernel.Core.Execution.ModelMessage']" />
public sealed record ModelMessage(
    string Role,
    string Content) : IModelMessage;
