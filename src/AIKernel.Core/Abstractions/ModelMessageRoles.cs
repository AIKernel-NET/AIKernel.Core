#pragma warning disable IDE0130 // Namespace がフォルダー構造と一致しません
namespace AIKernel.Dtos.Execution;
#pragma warning restore IDE0130 // Namespace がフォルダー構造と一致しません

public static class ModelMessageRoles
{
    public const string System = "system";

    public const string Developer = "developer";

    public const string User = "user";

    public const string Assistant = "assistant";

    public const string Tool = "tool";
}
