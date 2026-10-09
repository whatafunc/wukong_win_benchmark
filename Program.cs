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
            ║  CPU-test + GPU-test  |  OCR results extraction          ║
            ╚══════════════════════════════════════════════════════════╝
            """);

        bool noPrompt = args.Any(a => a is "--no-prompt" or "-q");
        bool skipOcr = args.Any(a => a is "--no-ocr");

        // tessdata next to the executable (or ./tessdata when running from project)
        string tessdataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
        if (!Directory.Exists(tessdataPath))
            tessdataPath = Path.GetFullPath("./tessdata");

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
        Console.WriteLine($"OCR tessdata      : {tessdataPath}  (exists={Directory.Exists(tessdataPath)})");

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

        string? cpuShot = null;
        if (!skipOcr)
        {
            Console.WriteLine("[CPU] Capturing screenshot for OCR…");
            cpuShot = ScreenshotHelper.CapturePrimaryToTemp("wukong_cpu");
        }
        ResultsParser.Enrich(cpuResult, installDir, cpuShot, tessdataPath, interactive: !noPrompt);

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

        string? gpuShot = null;
        if (!skipOcr)
        {
            Console.WriteLine("[GPU] Capturing screenshot for OCR…");
            gpuShot = ScreenshotHelper.CapturePrimaryToTemp("wukong_gpu");
        }
        ResultsParser.Enrich(gpuResult, installDir, gpuShot, tessdataPath, interactive: !noPrompt);

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

        await File.WriteAllTextAsync(reportPath, $"""
            Black Myth: Wukong Benchmark Automator – Report
            Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}

            {sysInfo}

            {cpuResult}

            {gpuResult}
            """);
        Console.WriteLine();
        Console.WriteLine($"Report saved to: {reportPath}");

        Console.WriteLine();
        Console.WriteLine("── Summary ──────────────────────────────────────────────");
        Console.WriteLine($"  CPU  Avg={Fmt(cpuResult.AverageFps)}  Max={Fmt(cpuResult.MaxFps)}  Min={Fmt(cpuResult.MinFps)}  5%Low={Fmt(cpuResult.OnePercentLow)}");
        Console.WriteLine($"  GPU  Avg={Fmt(gpuResult.AverageFps)}  Max={Fmt(gpuResult.MaxFps)}  Min={Fmt(gpuResult.MinFps)}  5%Low={Fmt(gpuResult.OnePercentLow)}");
        Console.WriteLine("────────────────────────────────────────────────────────");

        return 0;
    }

    private static string Fmt(double? v) => v.HasValue ? v.Value.ToString("F0") : "n/a";

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
                    catch { }
                    finally { p.Dispose(); }
                }
            }
        }
        catch { }
    }
}
