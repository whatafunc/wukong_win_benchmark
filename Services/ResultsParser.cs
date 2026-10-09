using System.Text.RegularExpressions;
using WukongBenchmarkAutomator.Models;

namespace WukongBenchmarkAutomator.Services;

/// <summary>
/// Fills BenchmarkResult numbers in this order:
///   1. OCR from screenshot (left column + regex)
///   2. Log files (best-effort)
///   3. Manual keyboard entry (fallback so report is never empty)
/// </summary>
public static class ResultsParser
{
    /// <param name="screenshotPath">Full path to PNG of results screen (or null).</param>
    /// <param name="tessdataPath">Folder containing eng.traineddata.</param>
    /// <param name="interactive">If true, ask user to confirm/fix numbers.</param>
    public static void Enrich(
        BenchmarkResult result,
        string installDir,
        string? screenshotPath = null,
        string? tessdataPath = null,
        bool interactive = true)
    {
        bool gotOcr = false;

        // 1. OCR
        if (!string.IsNullOrEmpty(screenshotPath) && !string.IsNullOrEmpty(tessdataPath))
        {
            Console.WriteLine($"[{result.Label}] Running OCR…");
            gotOcr = OcrResultsParser.TryFillFromScreenshot(result, screenshotPath, tessdataPath);
            if (gotOcr)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[{result.Label}] OCR OK  Avg={result.AverageFps}  Max={result.MaxFps}  Min={result.MinFps}  5%/1%Low={result.OnePercentLow}");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"[{result.Label}] OCR did not get Average FPS – will try logs / manual.");
                Console.ResetColor();
            }
        }

        // 2. Logs
        if (!gotOcr)
            TryParseFromLogs(result, installDir);

        // 3. Manual fallback / confirmation
        if (interactive)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            if (gotOcr)
            {
                Console.WriteLine($">>> [{result.Label}] OCR values above. Press Enter to keep a value, or type a new number.");
            }
            else
            {
                Console.WriteLine($">>> [{result.Label}] Type the numbers from the results screen (Enter = empty).");
            }
            Console.ResetColor();

            result.AverageFps = AskDouble("Average FPS", result.AverageFps);
            result.MaxFps = AskDouble("Max FPS", result.MaxFps);
            result.MinFps = AskDouble("Min FPS", result.MinFps);
            result.OnePercentLow = AskDouble("5% / 1% Low", result.OnePercentLow);

            if (result.AverageFps.HasValue || result.MinFps.HasValue)
                result.Success = true;

            if (string.IsNullOrEmpty(result.RawText))
                result.RawText = gotOcr
                    ? "(OCR + manual confirm)"
                    : "(manual entry)";
        }
        else if (!result.Success && !gotOcr)
        {
            result.RawText = "[ResultsParser] No OCR/logs. Re-run without --no-prompt or fix tessdata.";
        }
    }

    private static double? AskDouble(string label, double? current)
    {
        var hint = current.HasValue ? $" [{current.Value:F0}]" : "";
        Console.Write($"  {label}{hint}: ");
        var line = Console.ReadLine()?.Trim();
        if (string.IsNullOrWhiteSpace(line))
            return current;

        if (double.TryParse(line.Replace(',', '.'),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture,
                out var v))
            return v;

        Console.WriteLine("    (invalid – kept previous)");
        return current;
    }

    private static bool TryParseFromLogs(BenchmarkResult result, string installDir)
    {
        var candidates = new List<string>();
        var logsDir = Path.Combine(installDir, "b1", "Saved", "Logs");
        if (Directory.Exists(logsDir))
            candidates.AddRange(Directory.GetFiles(logsDir, "*.log"));

        try
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appDataLogs = Path.Combine(local, "b1", "Saved", "Logs");
            if (Directory.Exists(appDataLogs))
                candidates.AddRange(Directory.GetFiles(appDataLogs, "*.log"));
        }
        catch { /* ignore */ }

        foreach (var log in candidates.OrderByDescending(File.GetLastWriteTimeUtc).Take(8))
        {
            try
            {
                var text = File.ReadAllText(log);
                if (OcrResultsParser.ApplyParsedText(result, text) || ParseFpsFromText(text, result))
                {
                    result.RawText = $"(parsed from {Path.GetFileName(log)})";
                    return true;
                }
            }
            catch { /* skip */ }
        }
        return false;
    }

    public static bool ParseFpsFromText(string text, BenchmarkResult result)
    {
        bool found = false;
        var avg = MatchFirst(text,
            @"Average\s*FPS[:\s]+([\d.,]+)",
            @"Avg\.?\s*FPS[:\s]+([\d.,]+)",
            @"среднем[^\d]{0,15}([\d.,]+)");
        if (avg.HasValue) { result.AverageFps = avg; found = true; }

        var min = MatchFirst(text,
            @"Min(?:imum)?\s*FPS[:\s]+([\d.,]+)",
            @"Минимум[^\d]{0,15}([\d.,]+)");
        if (min.HasValue) { result.MinFps = min; found = true; }

        var max = MatchFirst(text,
            @"Max(?:imum)?\s*FPS[:\s]+([\d.,]+)",
            @"Максимум[^\d]{0,15}([\d.,]+)");
        if (max.HasValue) { result.MaxFps = max; found = true; }

        var onePct = MatchFirst(text,
            @"1\s*%\s*Low[:\s]+([\d.,]+)",
            @"5[- ]?й\s*перцентил\w*[^\d]{0,15}([\d.,]+)");
        if (onePct.HasValue) { result.OnePercentLow = onePct; found = true; }

        if (found) result.Success = true;
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
