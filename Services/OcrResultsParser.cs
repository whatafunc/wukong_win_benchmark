using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Text.RegularExpressions;
using Tesseract;
using WukongBenchmarkAutomator.Models;

namespace WukongBenchmarkAutomator.Services;

/// <summary>
/// OCR of the Wukong Benchmark results screen.
/// Strategy: crop the left column ("Результаты теста"), upscale + binarize,
/// run Tesseract, parse Average / Max / Min / 5% Low with regex (RU + EN).
/// </summary>
public static class OcrResultsParser
{
    /// <summary>
    /// Try to fill <paramref name="result"/> FPS fields from a screenshot.
    /// Returns true if at least Average FPS was read.
    /// </summary>
    public static bool TryFillFromScreenshot(BenchmarkResult result, string imagePath, string tessdataPath)
    {
        if (!File.Exists(imagePath))
        {
            Console.WriteLine($"[OCR] Image not found: {imagePath}");
            return false;
        }
        if (!Directory.Exists(tessdataPath) || !File.Exists(Path.Combine(tessdataPath, "eng.traineddata")))
        {
            Console.WriteLine($"[OCR] tessdata/eng.traineddata not found in: {tessdataPath}");
            Console.WriteLine("      Put eng.traineddata into a 'tessdata' folder next to the exe.");
            Console.WriteLine("      Download: https://github.com/tesseract-ocr/tessdata_fast/raw/main/eng.traineddata");
            return false;
        }

        try
        {
            using var original = new Bitmap(imagePath);
            using var column = CropLeftResultsColumn(original);
            using var processed = Preprocess(column);

            try
            {
                var debugPath = Path.Combine(
                    Path.GetDirectoryName(imagePath) ?? ".",
                    $"wukong_ocr_debug_{result.Label}.png");
                processed.Save(debugPath, System.Drawing.Imaging.ImageFormat.Png);
                Console.WriteLine($"[OCR] Debug crop: {debugPath}");
            }
            catch { /* ignore */ }

            using var engine = new TesseractEngine(tessdataPath, "eng", EngineMode.Default);
            engine.SetVariable("tessedit_char_whitelist",
                "0123456789.,FPS%fpSlowMinMaxAvgAveragePercentile ");

            using var ms = new MemoryStream();
            processed.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            using var pix = Pix.LoadFromMemory(ms.ToArray());
            using var page = engine.Process(pix, PageSegMode.Auto);

            string text = page.GetText() ?? "";
            Console.WriteLine("[OCR] Raw text:");
            Console.WriteLine(text.Trim());
            Console.WriteLine("---");

            return ApplyParsedText(result, text);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[OCR] Error: {ex.Message}");
            return false;
        }
    }

    /// <summary>Left ~38% of the image — "Результаты теста" column.</summary>
    private static Bitmap CropLeftResultsColumn(Bitmap full)
    {
        int x = 0;
        int y = (int)(full.Height * 0.12);
        int w = (int)(full.Width * 0.38);
        int h = (int)(full.Height * 0.70);

        w = Math.Min(w, full.Width - x);
        h = Math.Min(h, full.Height - y);
        if (w < 50 || h < 50)
        {
            x = 0; y = 0; w = full.Width; h = full.Height;
        }

        return full.Clone(new Rectangle(x, y, w, h), PixelFormat.Format32bppArgb);
    }

    private static Bitmap Preprocess(Bitmap src)
    {
        int nw = Math.Max(1, (int)(src.Width * 2.5));
        int nh = Math.Max(1, (int)(src.Height * 2.5));
        var resized = new Bitmap(nw, nh);
        using (var g = Graphics.FromImage(resized))
        {
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(src, 0, 0, nw, nh);
        }

        for (int y = 0; y < resized.Height; y++)
        {
            for (int x = 0; x < resized.Width; x++)
            {
                Color c = resized.GetPixel(x, y);
                int brightness = (int)(c.R * 0.3 + c.G * 0.59 + c.B * 0.11);
                resized.SetPixel(x, y, brightness > 100 ? Color.White : Color.Black);
            }
        }
        return resized;
    }

    public static bool ApplyParsedText(BenchmarkResult result, string text)
    {
        text = text.Replace('\n', ' ').Replace('\r', ' ');
        text = Regex.Replace(text, @"\s+", " ");

        var allNumbers = Regex.Matches(text, @"\b(\d{2,3})\b")
            .Select(m => int.Parse(m.Groups[1].Value))
            .Where(n => n >= 10 && n <= 999)
            .ToList();

        result.AverageFps = MatchLabeled(text,
            @"(?:В\s*среднем|Average|Avg\.?)\s*[^\d]{0,20}(\d{2,3})",
            @"(?:Средн\w*)\s*[^\d]{0,15}(\d{2,3})")
            ?? (allNumbers.Count > 0 ? allNumbers[0] : null);

        result.MaxFps = MatchLabeled(text,
            @"(?:Максимум|Maximum|Max\.?)\s*[^\d]{0,20}(\d{2,3})");

        result.MinFps = MatchLabeled(text,
            @"(?:Минимум|Minimum|Min\.?)\s*[^\d]{0,20}(\d{2,3})");

        // 5-й перцентиль ≈ 5% low (tool label); also accept 1% Low
        result.OnePercentLow = MatchLabeled(text,
            @"(?:5[- ]?(?:й|th)?\s*перцентил\w*|5\s*%\s*Low|1\s*%\s*Low|Percentile)\s*[^\d]{0,20}(\d{2,3})");

        if (allNumbers.Count >= 4 && result.AverageFps.HasValue && !result.MaxFps.HasValue)
        {
            result.MaxFps ??= allNumbers.Skip(1).FirstOrDefault();
            result.MinFps ??= allNumbers.Skip(2).FirstOrDefault();
            result.OnePercentLow ??= allNumbers.Skip(3).FirstOrDefault();
        }
        else if (allNumbers.Count >= 3 && result.AverageFps.HasValue)
        {
            var rest = allNumbers.Where(n => n != (int)result.AverageFps.Value).ToList();
            if (!result.MaxFps.HasValue && rest.Count > 0) result.MaxFps = rest[0];
            if (!result.MinFps.HasValue && rest.Count > 1) result.MinFps = rest[1];
            if (!result.OnePercentLow.HasValue && rest.Count > 2) result.OnePercentLow = rest[2];
        }

        result.RawText = text.Length > 240 ? text[..240] + "…" : text;
        bool ok = result.AverageFps.HasValue && result.AverageFps > 0;
        if (ok) result.Success = true;
        return ok;
    }

    private static double? MatchLabeled(string text, params string[] patterns)
    {
        foreach (var p in patterns)
        {
            var m = Regex.Match(text, p, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (m.Success && double.TryParse(m.Groups[1].Value, out var v) && v >= 10 && v <= 999)
                return v;
        }
        return null;
    }
}
