using System.Text.RegularExpressions;
using WukongBenchmarkAutomator.Models;

namespace WukongBenchmarkAutomator.Services;

/// <summary>
/// Stub + simple log-based parser for benchmark results.
/// 
/// The official Benchmark Tool only shows results on-screen.
/// This class therefore:
///   1. Looks for any *.log files under b1\Saved\Logs that appeared
///      after the run and tries to extract FPS numbers with regex.
///   2. Falls back to a clear message telling the user that OCR
///      (Tesseract / Windows.Media.Ocr) can be plugged in here.
/// 
/// Replace the body of TryParseFromLogs / TryParseFromScreenshot
/// with real OCR when you want fully automatic number extraction.
/// </summary>
public static class ResultsParser
{
    public static void Enrich(BenchmarkResult result, string installDir, bool interactive = true)
    {
        // 1. Try logs first (best-effort)
        var fromLogs = TryParseFromLogs(result, installDir);

        // 2. Ask user for the numbers shown on screen
        if (interactive)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($">>> [{result.Label}] Look at the Benchmark Tool results screen and enter the numbers below.");
            Console.WriteLine("    (Press Enter to leave a field empty)");
            Console.ResetColor();

            result.AverageFps       = AskDouble("Average FPS", result.AverageFps);
            result.MinFps           = AskDouble("Min FPS", result.MinFps);
            result.MaxFps           = AskDouble("Max FPS", result.MaxFps);
            result.OnePercentLow    = AskDouble("1% Low", result.OnePercentLow);
            result.PointOnePercentLow = AskDouble("0.1% Low", result.PointOnePercentLow);

            if (result.AverageFps.HasValue || result.MinFps.HasValue)
                result.Success = true;

            result.RawText = fromLogs
                ? "(numbers from logs + manual confirmation)"
                : "(numbers entered manually from on-screen results)";
        }
        else if (!fromLogs)
        {
            result.RawText = "[ResultsParser] No numbers found in logs. Re-run without --no-prompt.";
        }
    }

    private static bool TryParseFromLogs(BenchmarkResult result, string installDir)
    {
        var logsDir = Path.Combine(installDir, "b1", "Saved", "Logs");
        if (!Directory.Exists(logsDir))
            return false;

        // Newest log files first
        var logs = Directory.GetFiles(logsDir, "*.log")
                            .OrderByDescending(File.GetLastWriteTimeUtc)
                            .Take(5);

        foreach (var log in logs)
        {
            try
            {
                var text = File.ReadAllText(log);
                if (ParseFpsFromText(text, result))
                {
                    result.RawText = $"(parsed from {Path.GetFileName(log)})";
                    return true;
                }
            }
            catch { /* skip unreadable logs */ }
        }
        return false;
    }

    /// <summary>
    /// Very tolerant regex extraction. Works on both English and Chinese
    /// result screens / log dumps that contain typical FPS phrases.
    /// </summary>
    public static bool ParseFpsFromText(string text, BenchmarkResult result)
    {
        bool found = false;

        // Average / Avg FPS
        var avg = MatchFirst(text,
            @"Average\s*FPS[:\s]+([\d.,]+)",
            @"Avg\.?\s*FPS[:\s]+([\d.,]+)",
            @"平均\s*FPS[:\s]+([\d.,]+)",
            @"平均帧率[:\s]+([\d.,]+)");
        if (avg.HasValue) { result.AverageFps = avg; found = true; }

        // Min FPS
        var min = MatchFirst(text,
            @"Min(?:imum)?\s*FPS[:\s]+([\d.,]+)",
            @"最低\s*FPS[:\s]+([\d.,]+)",
            @"最小帧率[:\s]+([\d.,]+)");
        if (min.HasValue) { result.MinFps = min; found = true; }

        // Max FPS
        var max = MatchFirst(text,
            @"Max(?:imum)?\s*FPS[:\s]+([\d.,]+)",
            @"最高\s*FPS[:\s]+([\d.,]+)",
            @"最大帧率[:\s]+([\d.,]+)");
        if (max.HasValue) { result.MaxFps = max; found = true; }

        // 1% Low
        var onePct = MatchFirst(text,
            @"1\s*%\s*Low[:\s]+([\d.,]+)",
            @"1%\s*低[:\s]+([\d.,]+)");
        if (onePct.HasValue) { result.OnePercentLow = onePct; found = true; }

        // 0.1% Low
        var pointOne = MatchFirst(text,
            @"0\.1\s*%\s*Low[:\s]+([\d.,]+)",
            @"0\.1%\s*低[:\s]+([\d.,]+)");
        if (pointOne.HasValue) { result.PointOnePercentLow = pointOne; found = true; }

        return found;
    }

    private static double? MatchFirst(string text, params string[] patterns)
    {
        foreach (var p in patterns)
        {
            var m = Regex.Match(text, p, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (m.Success && double.TryParse(m.Groups[1].Value.Replace(',', '.'),
                                             System.Globalization.NumberStyles.Any,
                                             System.Globalization.CultureInfo.InvariantCulture,
                                             out var v))
                return v;
        }
        return null;
    }

    private static double? AskDouble(string label, double? current)
    {
        var hint = current.HasValue ? $" [{current.Value:F1}]" : "";
        Console.Write($"  {label}{hint}: ");
        var line = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(line))
            return current;

        if (double.TryParse(line.Replace(',', '.'),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var v))
            return v;

        Console.WriteLine("    (invalid number – kept previous value)");
        return current;
    }
}
