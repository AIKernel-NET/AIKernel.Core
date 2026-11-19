using AIKernel.Common.Results;
using AIKernel.Core.Governance;

namespace AIKernel.Kernel.Governance;

/// <summary>Applies temperature and batch power limits to a telemetry sample.</summary>
public static class DeviceHealthPolicy
{
    private static readonly ErrorContext Hot = new("The GPU temperature exceeds its configured limit.",
        "device_temperature_exceeded", true) { FailureKind = FailureKind.FailClosed };
    private static readonly ErrorContext Power = new("Batch execution is throttled by the device power limit.",
        "device_power_throttled", true) { FailureKind = FailureKind.FailClosed };
    private static readonly ErrorContext Unavailable = new("Required device telemetry is unavailable.",
        "device_telemetry_unavailable", true) { FailureKind = FailureKind.FailClosed };

    /// <summary>Returns the sample when allowed, or a routing error when the device cannot be used.</summary>
    public static Result<DeviceHealthSnapshot> Evaluate(Result<DeviceHealthSnapshot> health, DeviceHealthLimits limits,
        InferencePriority priority)
    {
        if (!health.IsSuccess) return health;

        var sample = health.Value;
        if (limits.MaximumTemperatureCelsius > 0 && sample.TemperatureCelsius > limits.MaximumTemperatureCelsius)
            return Result<DeviceHealthSnapshot>.Fail(Hot);
        if (priority == InferencePriority.BackgroundBatch && limits.BatchPowerLimitPercent > 0)
        {
            if (sample.PowerMilliwatts is not { } power || sample.PowerLimitMilliwatts is not (> 0) ||
                limits.BatchPowerLimitPercent > 100)
                return Result<DeviceHealthSnapshot>.Fail(Unavailable);
            if ((ulong)power * 100 >= (ulong)sample.PowerLimitMilliwatts.Value * limits.BatchPowerLimitPercent)
                return Result<DeviceHealthSnapshot>.Fail(Power);
        }
        return health;
    }
}
