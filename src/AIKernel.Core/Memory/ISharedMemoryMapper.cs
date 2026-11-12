using AIKernel.Common.Results;

namespace AIKernel.Core.Memory;

/// <summary>Attaches to a named region created by a cooperating local process.</summary>
public interface ISharedMemoryMapper : IMemoryMapper
{
    /// <summary>Maps the requested bytes of an existing channel for direct access.</summary>
    Result<IMemoryRegion> OpenShared(string channelName, long capacity);
}
