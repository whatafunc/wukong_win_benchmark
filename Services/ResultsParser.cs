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
    public static void Enrich(BenchmarkResult result, string installDir)
    {
        if (!result.Success) return;

        // 1. Try log files
        if (TryParseFromLogs(result, installDir))
            return;

        // 2. Placeholder for screenshot + OCR
        result.RawText = """
            [ResultsParser] No numeric results found in log files.
            The Benchmark Tool displays results only on-screen.

            To enable automatic extraction:
              • Add the Tesseract NuGet package
              • Capture the results window (or full desktop) after the run
              • Run OCR and feed the text into ParseFpsFromText()

            For now the settings used for this pass are still recorded above.
            """;
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
}
