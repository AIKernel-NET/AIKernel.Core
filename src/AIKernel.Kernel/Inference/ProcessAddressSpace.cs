namespace AIKernel.Kernel.Inference;

internal static class ProcessAddressSpace
{
    internal static long? ReadAddressSpaceLimit()
    {
        const string path="/proc/self/limits", label="Max address space";
        if(!OperatingSystem.IsLinux() || !File.Exists(path))return null;
        foreach(string line in File.ReadLines(path))
        {
            if(!line.StartsWith(label,StringComparison.Ordinal))continue;
            var value=line.AsSpan(label.Length).TrimStart();
            int end=value.IndexOf(' ');
            if(end>0 && long.TryParse(value[..end],out long limit))return limit;
        }
        return null;
    }
}
