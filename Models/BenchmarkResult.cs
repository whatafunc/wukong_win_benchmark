namespace WukongBenchmarkAutomator.Models;

public sealed class BenchmarkResult
{
    public string Label { get; init; } = "";          // "CPU" or "GPU"
    public double? AverageFps { get; set; }
    public double? MinFps { get; set; }
    public double? MaxFps { get; set; }
    public double? OnePercentLow { get; set; }
    public double? PointOnePercentLow { get; set; }
    public string RawText { get; set; } = "";         // OCR / log dump
    public string SettingsSummary { get; set; } = "";
    public bool Success { get; set; }
    public string? Error { get; set; }

    public override string ToString()
    {
        if (!Success)
            return $"[{Label}] FAILED: {Error}";

        return $"""
            === {Label}-test results ===
            Settings used :
            {SettingsSummary}
            Average FPS   : {AverageFps?.ToString("F1") ?? "n/a"}
            Min FPS       : {MinFps?.ToString("F1") ?? "n/a"}
            Max FPS       : {MaxFps?.ToString("F1") ?? "n/a"}
            1% Low        : {OnePercentLow?.ToString("F1") ?? "n/a"}
            0.1% Low      : {PointOnePercentLow?.ToString("F1") ?? "n/a"}
            """;
    }
}
