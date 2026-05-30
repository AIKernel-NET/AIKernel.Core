namespace AIKernel.Common.Logging;

public static class LogUtil
{
    public static void Info(string message)
        => Write("INFO", message);

    public static void Warn(string message)
        => Write("WARN", message);

    public static void Error(string message)
        => Write("ERROR", message);

    private static void Write(string level, string message)
    {
        var timestamp = DateTime.UtcNow.ToString("o");
        Console.WriteLine($"[{timestamp}] [{level}] {message}");
    }
}
