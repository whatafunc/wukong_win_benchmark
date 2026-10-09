using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace WukongBenchmarkAutomator.Services;

/// <summary>
/// Captures the primary screen (or virtual screen) to a PNG file.
/// </summary>
public static class ScreenshotHelper
{
    public static string CapturePrimaryToTemp(string tag = "wukong")
    {
        var bounds = GetVirtualScreenBounds();
        using var bmp = new Bitmap(bounds.Width, bounds.Height);
        using (var g = Graphics.FromImage(bmp))
        {
            g.CopyFromScreen(bounds.X, bounds.Y, 0, 0, bounds.Size);
        }

        string path = Path.Combine(
            Path.GetTempPath(),
            $"{tag}_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine($"[Screenshot] Saved: {path}");
        return path;
    }

    private static Rectangle GetVirtualScreenBounds()
    {
        // Prefer full virtual screen (multi-monitor)
        try
        {
            int x = GetSystemMetrics(76);  // SM_XVIRTUALSCREEN
            int y = GetSystemMetrics(77);  // SM_YVIRTUALSCREEN
            int w = GetSystemMetrics(78);  // SM_CXVIRTUALSCREEN
            int h = GetSystemMetrics(79);  // SM_CYVIRTUALSCREEN
            if (w > 0 && h > 0)
                return new Rectangle(x, y, w, h);
        }
        catch { /* ignore */ }

        return new Rectangle(0, 0,
            GetSystemMetrics(0),   // SM_CXSCREEN
            GetSystemMetrics(1));  // SM_CYSCREEN
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);
}
