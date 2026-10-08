namespace WukongBenchmarkAutomator.Models;

public sealed class SystemInfo
{
    public string CpuName { get; init; } = "Unknown";
    public int CpuCores { get; init; }
    public int CpuLogicalProcessors { get; init; }
    public string GpuName { get; init; } = "Unknown";
    public string GpuDriverVersion { get; init; } = "Unknown";
    public long GpuVramMb { get; init; }
    public long TotalRamMb { get; init; }
    public string OsVersion { get; init; } = "Unknown";
    public string MachineName { get; init; } = Environment.MachineName;

    public override string ToString()
    {
        return $"""
            === System Information ===
            Machine   : {MachineName}
            OS        : {OsVersion}
            CPU       : {CpuName}
                        Cores: {CpuCores}  |  Logical: {CpuLogicalProcessors}
            GPU       : {GpuName}
                        Driver: {GpuDriverVersion}  |  VRAM: {GpuVramMb} MB
            RAM       : {TotalRamMb} MB
            """;
    }
}
