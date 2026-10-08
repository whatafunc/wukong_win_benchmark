using WukongBenchmarkAutomator.Models;
using WukongBenchmarkAutomator.Services;

namespace WukongBenchmarkAutomator;

internal static class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("""
            ╔══════════════════════════════════════════════════════════╗
            ║  Black Myth: Wukong Benchmark Automator  (.NET 8)        ║
            ║  CPU-test + GPU-test in one automatic run                ║
            ╚══════════════════════════════════════════════════════════╝
            """);

        // ------------------------------------------------------------------
        // 1. Locate the Benchmark Tool
        // ------------------------------------------------------------------
        var installDir = SteamLocator.FindInstallDirectory();
        if (installDir is null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("ERROR: Black Myth: Wukong Benchmark Tool not found.");
            Console.WriteLine("Install it from Steam (AppID 3132990) or set the");
            Console.WriteLine("environment variable WUKONG_BENCHMARK_PATH to its folder.");
            Console.ResetColor();
            return 1;
        }
        Console.WriteLine($"Install directory : {installDir}");

        // ------------------------------------------------------------------
        // 2. System information
        // ------------------------------------------------------------------
        var sysInfo = SystemInfoCollector.Collect();
        Console.WriteLine();
        Console.WriteLine(sysInfo);

        // Default resolution for GPU profile (override with --width / --height)
        int screenW = 1920, screenH = 1080;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--width" && int.TryParse(args[i + 1], out var w)) screenW = w;
            if (args[i] == "--height" && int.TryParse(args[i + 1], out var h)) screenH = h;
        }
        Console.WriteLine($"GPU-test resolution : {screenW}x{screenH}  (change with --width / --height)");

        var runner = new BenchmarkRunner(installDir);

        // ------------------------------------------------------------------
        // 3. CPU-test
        // ------------------------------------------------------------------
        Console.WriteLine();
        Console.WriteLine(">>> Starting CPU-test (low resolution, lowest quality)…");
        var cpuProfile = BenchmarkProfile.CpuTest;
        var cpuResult = await runner.RunAsync("CPU", cpuProfile);
        ResultsParser.Enrich(cpuResult, installDir);

        // ------------------------------------------------------------------
        // 4. GPU-test
        // ------------------------------------------------------------------
        Console.WriteLine();
        Console.WriteLine(">>> Starting GPU-test (high resolution, maximum quality)…");
        var gpuProfile = BenchmarkProfile.GpuTest(screenW, screenH);
        var gpuResult = await runner.RunAsync("GPU", gpuProfile);
        ResultsParser.Enrich(gpuResult, installDir);

        // ------------------------------------------------------------------
        // 5. Final report
        // ------------------------------------------------------------------
        Console.WriteLine();
        Console.WriteLine("══════════════════════════════════════════════════════════");
        Console.WriteLine("                     FINAL REPORT");
        Console.WriteLine("══════════════════════════════════════════════════════════");
        Console.WriteLine(sysInfo);
        Console.WriteLine(cpuResult);
        Console.WriteLine(gpuResult);

        // Also write a timestamped text file next to the executable
        var reportPath = Path.Combine(
            AppContext.BaseDirectory,
            $"WukongBenchmark_Report_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        await File.WriteAllTextAsync(reportPath, $"""
            Black Myth: Wukong Benchmark Automator – Report
            Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}

            {sysInfo}

            {cpuResult}

            {gpuResult}
            """);
        Console.WriteLine();
        Console.WriteLine($"Report saved to: {reportPath}");

        return (cpuResult.Success && gpuResult.Success) ? 0 : 2;
    }
}
