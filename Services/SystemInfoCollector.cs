using System.Management;
using WukongBenchmarkAutomator.Models;

namespace WukongBenchmarkAutomator.Services;

public static class SystemInfoCollector
{
    public static SystemInfo Collect()
    {
        return new SystemInfo
        {
            CpuName = GetCpuName(),
            CpuCores = Environment.ProcessorCount,          // logical; refined below
            CpuLogicalProcessors = Environment.ProcessorCount,
            GpuName = GetGpuName(),
            GpuDriverVersion = GetGpuDriver(),
            GpuVramMb = GetGpuVramMb(),
            TotalRamMb = GetTotalRamMb(),
            OsVersion = Environment.OSVersion.ToString(),
            MachineName = Environment.MachineName
        };
    }

    private static string GetCpuName()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");
            foreach (ManagementObject obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString()?.Trim() ?? "Unknown CPU";
                // Prefer the first processor
                return name;
            }
        }
        catch { /* ignore */ }
        return "Unknown CPU";
    }

    private static string GetGpuName()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_VideoController");
            var names = new List<string>();
            foreach (ManagementObject obj in searcher.Get())
            {
                var n = obj["Name"]?.ToString();
                if (!string.IsNullOrWhiteSpace(n) && !n.Contains("Microsoft Basic", StringComparison.OrdinalIgnoreCase))
                    names.Add(n);
            }
            // Prefer discrete GPU if multiple
            return names.FirstOrDefault() ?? "Unknown GPU";
        }
        catch { /* ignore */ }
        return "Unknown GPU";
    }

    private static string GetGpuDriver()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT DriverVersion FROM Win32_VideoController");
            foreach (ManagementObject obj in searcher.Get())
            {
                var v = obj["DriverVersion"]?.ToString();
                if (!string.IsNullOrWhiteSpace(v))
                    return v;
            }
        }
        catch { /* ignore */ }
        return "Unknown";
    }

    private static long GetGpuVramMb()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT AdapterRAM FROM Win32_VideoController");
            foreach (ManagementObject obj in searcher.Get())
            {
                if (obj["AdapterRAM"] is uint ram && ram > 0)
                    return ram / (1024 * 1024);
            }
        }
        catch { /* ignore */ }
        return 0;
    }

    private static long GetTotalRamMb()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
            foreach (ManagementObject obj in searcher.Get())
            {
                if (obj["TotalPhysicalMemory"] is ulong bytes)
                    return (long)(bytes / (1024 * 1024));
            }
        }
        catch { /* ignore */ }
        return 0;
    }
}
