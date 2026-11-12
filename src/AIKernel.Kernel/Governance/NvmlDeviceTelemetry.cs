using System.Runtime.InteropServices;
using System.Text;
using AIKernel.Common.Results;
using AIKernel.Core.Governance;

namespace AIKernel.Kernel.Governance;

/// <summary>Queries NVIDIA driver telemetry without linking the monitoring library at build time.</summary>
public sealed unsafe class NvmlDeviceTelemetry : IDeviceHealthTelemetry, IDisposable
{
    private static readonly ErrorContext Unavailable = new("NVML or the requested device telemetry is unavailable.",
        "device_telemetry_unavailable", true) { FailureKind = FailureKind.FailClosed };
    private readonly Lock gate = new();
    private nint library;
    private readonly delegate* unmanaged<int> init;
    private readonly delegate* unmanaged<int> shutdown;
    private readonly delegate* unmanaged<byte*, nint*, int> getDevice;
    private readonly delegate* unmanaged<nint, uint, uint*, int> temperature;
    private readonly delegate* unmanaged<nint, MemoryInfo*, int> memory;
    private readonly delegate* unmanaged<nint, uint*, int> power;
    private readonly delegate* unmanaged<nint, uint*, int> powerLimit;
    private readonly delegate* unmanaged<nint, uint, uint*, int> pcie;
    private readonly delegate* unmanaged<nint, uint, uint, ulong*, int> ecc;

    private NvmlDeviceTelemetry(nint library)
    {
        this.library = library;
        init = (delegate* unmanaged<int>)NativeLibrary.GetExport(library, "nvmlInit_v2");
        shutdown = (delegate* unmanaged<int>)NativeLibrary.GetExport(library, "nvmlShutdown");
        getDevice = (delegate* unmanaged<byte*, nint*, int>)NativeLibrary.GetExport(library, "nvmlDeviceGetHandleByPciBusId_v2");
        temperature = (delegate* unmanaged<nint, uint, uint*, int>)NativeLibrary.GetExport(library, "nvmlDeviceGetTemperature");
        memory = (delegate* unmanaged<nint, MemoryInfo*, int>)NativeLibrary.GetExport(library, "nvmlDeviceGetMemoryInfo");
        power = (delegate* unmanaged<nint, uint*, int>)Find(library, "nvmlDeviceGetPowerUsage");
        powerLimit = (delegate* unmanaged<nint, uint*, int>)Find(library, "nvmlDeviceGetEnforcedPowerLimit");
        pcie = (delegate* unmanaged<nint, uint, uint*, int>)Find(library, "nvmlDeviceGetPcieThroughput");
        ecc = (delegate* unmanaged<nint, uint, uint, ulong*, int>)Find(library, "nvmlDeviceGetTotalEccErrors");
    }

    /// <summary>Opens a driver telemetry session or returns an unavailable result.</summary>
    public static Result<NvmlDeviceTelemetry> Open()
    {
        nint handle = 0;
        try
        {
            if (OperatingSystem.IsWindows())
            {
                if (!NativeLibrary.TryLoad("nvml.dll", typeof(NvmlDeviceTelemetry).Assembly,
                    DllImportSearchPath.System32, out handle))
                {
                    var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                    if (Path.IsPathFullyQualified(programFiles))
                        NativeLibrary.TryLoad(Path.Combine(programFiles, "NVIDIA Corporation", "NVSMI", "nvml.dll"), out handle);
                }
            }
            else if (OperatingSystem.IsLinux())
            {
                NativeLibrary.TryLoad("libnvidia-ml.so.1", out handle);
            }

            if (handle == 0) return Result<NvmlDeviceTelemetry>.Fail(Unavailable);
            var telemetry = new NvmlDeviceTelemetry(handle);
            if (telemetry.init() != 0) return Result<NvmlDeviceTelemetry>.Fail(Unavailable);
            handle = 0;
            return Result<NvmlDeviceTelemetry>.Success(telemetry);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException
            or BadImageFormatException or NotSupportedException)
        {
            return Result<NvmlDeviceTelemetry>.Fail(Unavailable);
        }
        finally
        {
            if (handle != 0) NativeLibrary.Free(handle);
        }
    }

    /// <inheritdoc />
    public Result<DeviceHealthSnapshot> Read(string pciBusId)
    {
        if (pciBusId is null || pciBusId.Length is < 1 or > 31)
            return Result<DeviceHealthSnapshot>.Fail(Unavailable);
        foreach (var c in pciBusId)
            if (!char.IsAsciiHexDigit(c) && c is not (':' or '.'))
                return Result<DeviceHealthSnapshot>.Fail(Unavailable);

        Span<byte> encoded = stackalloc byte[32];
        var length = Encoding.ASCII.GetBytes(pciBusId, encoded);
        encoded[length] = 0;
        lock (gate)
        {
            nint device = 0;
            uint degrees = 0;
            MemoryInfo usage = default;
            fixed (byte* name = encoded)
            {
                if (library == 0 || getDevice(name, &device) != 0 || temperature(device, 0, &degrees) != 0 ||
                    memory(device, &usage) != 0)
                    return Result<DeviceHealthSnapshot>.Fail(Unavailable);
            }

            return Result<DeviceHealthSnapshot>.Success(new(degrees, ReadMetric(power, device), ReadMetric(powerLimit, device),
                usage.Used, usage.Total, usage.Free, ReadPcie(device, 1), ReadPcie(device, 0),
                ReadEcc(device, 0), ReadEcc(device, 1)));
        }
    }

    private static nint Find(nint library, string name) =>
        NativeLibrary.TryGetExport(library, name, out var value) ? value : 0;

    private static uint? ReadMetric(delegate* unmanaged<nint, uint*, int> query, nint device)
    {
        uint value = 0;
        return query != null && query(device, &value) == 0 ? value : null;
    }

    private uint? ReadPcie(nint device, uint counter)
    {
        uint value = 0;
        return pcie != null && pcie(device, counter, &value) == 0 ? value : null;
    }

    private ulong? ReadEcc(nint device, uint kind)
    {
        ulong value = 0;
        return ecc != null && ecc(device, kind, 1, &value) == 0 ? value : null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (gate)
        {
            if (library == 0) return;
            _ = shutdown();
            NativeLibrary.Free(library);
            library = 0;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryInfo
    {
        public ulong Total;
        public ulong Free;
        public ulong Used;
    }
}
