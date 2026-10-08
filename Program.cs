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
            ║  CPU-test + GPU-test in one run                          ║
            ╚══════════════════════════════════════════════════════════╝
            """);

        bool noPrompt = args.Any(a => a is "--no-prompt" or "-q");

        // ------------------------------------------------------------------
        // 1. Locate the Benchmark Tool
        // ------------------------------------------------------------------
        var installDir = SteamLocator.FindInstallDirectory();
        if (installDir is null)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("ERROR: Black Myth: Wukong Benchmark Tool not found.");
            Console.WriteLine("Install it from Steam (AppID 3132990) or set:");
            Console.WriteLine("  set WUKONG_BENCHMARK_PATH=C:\\path\\to\\Black Myth Wukong Benchmark Tool");
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

        int screenW = 1920, screenH = 1080;
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "--width" && int.TryParse(args[i + 1], out var w)) screenW = w;
            if (args[i] == "--height" && int.TryParse(args[i + 1], out var h)) screenH = h;
        }
        Console.WriteLine($"GPU-test resolution : {screenW}x{screenH}");

        var runner = new BenchmarkRunner(installDir);

        // ------------------------------------------------------------------
        // 3. CPU-test
        // ------------------------------------------------------------------
        Console.WriteLine();
        Console.WriteLine("══════════════════════════════════════════════════════════");
        Console.WriteLine("  CPU-TEST  (1280×720, all quality = Low, RT Off)");
        Console.WriteLine("══════════════════════════════════════════════════════════");
        var cpuProfile = BenchmarkProfile.CpuTest;
        var cpuResult = await runner.RunAsync("CPU", cpuProfile);
        ResultsParser.Enrich(cpuResult, installDir, interactive: !noPrompt);

        TryCloseBenchmarkProcesses();
        await Task.Delay(2000);

        // ------------------------------------------------------------------
        // 4. GPU-test
        // ------------------------------------------------------------------
        Console.WriteLine();
        Console.WriteLine("══════════════════════════════════════════════════════════");
        Console.WriteLine("  GPU-TEST  (max quality + RT, high resolution)");
        Console.WriteLine("══════════════════════════════════════════════════════════");
        var gpuProfile = BenchmarkProfile.GpuTest(screenW, screenH);
        var gpuResult = await runner.RunAsync("GPU", gpuProfile);
        ResultsParser.Enrich(gpuResult, installDir, interactive: !noPrompt);

        TryCloseBenchmarkProcesses();

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

        var reportPath = Path.Combine(
            AppContext.BaseDirectory,
            $"WukongBenchmark_Report_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

        var reportText = $"""
            Black Myth: Wukong Benchmark Automator – Report
            Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}

            {sysInfo}

            {cpuResult}

            {gpuResult}
            """;

        await File.WriteAllTextAsync(reportPath, reportText);
        Console.WriteLine();
        Console.WriteLine($"Report saved to: {reportPath}");

        Console.WriteLine();
        Console.WriteLine("── Summary ──────────────────────────────────────────────");
        Console.WriteLine($"  CPU  Avg={Fmt(cpuResult.AverageFps)}  Min={Fmt(cpuResult.MinFps)}  1%Low={Fmt(cpuResult.OnePercentLow)}");
        Console.WriteLine($"  GPU  Avg={Fmt(gpuResult.AverageFps)}  Min={Fmt(gpuResult.MinFps)}  1%Low={Fmt(gpuResult.OnePercentLow)}");
        Console.WriteLine("────────────────────────────────────────────────────────");

        return 0;
    }

    private static string Fmt(double? v) => v.HasValue ? v.Value.ToString("F1") : "n/a";

    private static void TryCloseBenchmarkProcesses()
    {
        try
        {
            foreach (var name in new[] { "b1_benchmark", "b1-Win64-Shipping", "b1" })
            {
                foreach (var p in System.Diagnostics.Process.GetProcessesByName(name))
                {
                    try
                    {
                        p.CloseMainWindow();
                        if (!p.WaitForExit(3000))
                            p.Kill(entireProcessTree: true);
                    }
                    catch { /* ignore */ }
                    finally { p.Dispose(); }
                }
            }
        }
        catch { /* ignore */ }
    }
}
