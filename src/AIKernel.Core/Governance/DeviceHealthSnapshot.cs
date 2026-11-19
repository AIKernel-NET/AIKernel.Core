using AIKernel.Common.Results;

namespace AIKernel.Core.Governance;

/// <summary>Physical device telemetry. Optional measurements may be unavailable on some hardware.</summary>
public readonly record struct DeviceHealthSnapshot(uint TemperatureCelsius, uint? PowerMilliwatts, uint? PowerLimitMilliwatts,
    ulong VramUsedBytes, ulong VramTotalBytes, ulong VramFreeBytes, uint? PcieRxKilobytesPerSecond,
    uint? PcieTxKilobytesPerSecond, ulong? CorrectedEccErrors, ulong? UncorrectedEccErrors);

/// <summary>Reads health measurements for a physical PCI device identifier.</summary>
public interface IDeviceHealthTelemetry
{
    /// <summary>Returns the available health measurements or a structured unavailable status.</summary>
    Result<DeviceHealthSnapshot> Read(string pciBusId);
}

/// <summary>Inference priority at an execution checkpoint.</summary>
public enum InferencePriority
{
    /// <summary>Deferred background work.</summary>
    BackgroundBatch,
    /// <summary>Regular inference.</summary>
    Normal,
    /// <summary>Latency-sensitive inference.</summary>
    High,
    /// <summary>Urgent inference that takes precedence at the next checkpoint.</summary>
    Emergency
}

/// <summary>Limits used to authorize execution on a device.</summary>
public readonly record struct DeviceHealthLimits(uint MaximumTemperatureCelsius, uint BatchPowerLimitPercent);
