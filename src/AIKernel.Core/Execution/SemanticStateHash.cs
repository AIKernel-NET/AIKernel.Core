namespace AIKernel.Core.Execution;

public readonly record struct SemanticStateHash(
    string Algorithm,
    string HexDigest)
{
    public override string ToString()
    {
        return $"{Algorithm}:{HexDigest}";
    }

    public string ToExecutionId()
    {
        return "exec:" + ToString();
    }
}
