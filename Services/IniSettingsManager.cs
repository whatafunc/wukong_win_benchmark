using System.Text;
using System.Text.RegularExpressions;

namespace WukongBenchmarkAutomator.Services;

/// <summary>
/// Reads / writes GameUserSettings.ini of the Benchmark Tool.
/// Path: &lt;install&gt;\b1\Saved\Config\Windows\GameUserSettings.ini
/// </summary>
public sealed class IniSettingsManager
{
    private readonly string _iniPath;

    public IniSettingsManager(string installDir)
    {
        _iniPath = Path.Combine(installDir, "b1", "Saved", "Config", "Windows", "GameUserSettings.ini");
    }

    public string IniPath => _iniPath;

    public bool Exists => File.Exists(_iniPath);

    /// <summary>
    /// Ensure the config directory exists (first launch creates it).
    /// </summary>
    public void EnsureDirectory()
    {
        var dir = Path.GetDirectoryName(_iniPath)!;
        Directory.CreateDirectory(dir);
    }

    public string ReadAll()
    {
        if (!File.Exists(_iniPath))
            return string.Empty;
        return File.ReadAllText(_iniPath, Encoding.UTF8);
    }

    public void WriteAll(string content)
    {
        // Remove read-only if present
        if (File.Exists(_iniPath))
        {
            var attrs = File.GetAttributes(_iniPath);
            if (attrs.HasFlag(FileAttributes.ReadOnly))
                File.SetAttributes(_iniPath, attrs & ~FileAttributes.ReadOnly);
        }

        File.WriteAllText(_iniPath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    public void SetReadOnly(bool readOnly)
    {
        if (!File.Exists(_iniPath)) return;
        var attrs = File.GetAttributes(_iniPath);
        if (readOnly)
            File.SetAttributes(_iniPath, attrs | FileAttributes.ReadOnly);
        else
            File.SetAttributes(_iniPath, attrs & ~FileAttributes.ReadOnly);
    }

    // ---------- High-level helpers ----------

    /// <summary>
    /// Applies a full set of scalability values (0 = Low … 4 = Cinematic).
    /// Also forces resolution and a few known keys that help isolate CPU / GPU.
    /// </summary>
    public void ApplyProfile(BenchmarkProfile profile)
    {
        EnsureDirectory();
        var text = ReadAll();

        // Create a minimal skeleton if the file does not exist yet
        if (string.IsNullOrWhiteSpace(text))
        {
            text = """
                [/Script/Engine.GameUserSettings]
                bUseVSync=False
                ResolutionSizeX=1920
                ResolutionSizeY=1080
                LastUserConfirmedResolutionSizeX=1920
                LastUserConfirmedResolutionSizeY=1080
                WindowPosX=-1
                WindowPosY=-1
                FullscreenMode=1
                LastConfirmedFullscreenMode=1
                PreferredFullscreenMode=1
                FrameRateLimit=0.000000
                DesiredScreenWidth=1920
                DesiredScreenHeight=1080
                bUseHDRDisplayOutput=False

                [ScalabilityGroups]
                sg.ResolutionQuality=100.000000
                sg.ViewDistanceQuality=3
                sg.AntiAliasingQuality=3
                sg.ShadowQuality=3
                sg.GlobalIlluminationQuality=3
                sg.ReflectionQuality=3
                sg.PostProcessQuality=3
                sg.TextureQuality=3
                sg.EffectsQuality=3
                sg.FoliageQuality=3
                sg.ShadingQuality=3
                sg.LandscapeQuality=3
                """;
        }

        // Resolution
        text = SetKey(text, "ResolutionSizeX", profile.Width.ToString());
        text = SetKey(text, "ResolutionSizeY", profile.Height.ToString());
        text = SetKey(text, "LastUserConfirmedResolutionSizeX", profile.Width.ToString());
        text = SetKey(text, "LastUserConfirmedResolutionSizeY", profile.Height.ToString());
        text = SetKey(text, "DesiredScreenWidth", profile.Width.ToString());
        text = SetKey(text, "DesiredScreenHeight", profile.Height.ToString());

        // VSync / frame limit
        text = SetKey(text, "bUseVSync", profile.VSync ? "True" : "False");
        text = SetKey(text, "FrameRateLimit", profile.FrameRateLimit.ToString("F6"));

        // Scalability groups
        text = SetScalability(text, "sg.ResolutionQuality", profile.ResolutionQuality);
        text = SetScalability(text, "sg.ViewDistanceQuality", profile.ViewDistance);
        text = SetScalability(text, "sg.AntiAliasingQuality", profile.AntiAliasing);
        text = SetScalability(text, "sg.ShadowQuality", profile.Shadow);
        text = SetScalability(text, "sg.GlobalIlluminationQuality", profile.GlobalIllumination);
        text = SetScalability(text, "sg.ReflectionQuality", profile.Reflection);
        text = SetScalability(text, "sg.PostProcessQuality", profile.PostProcess);
        text = SetScalability(text, "sg.TextureQuality", profile.Texture);
        text = SetScalability(text, "sg.EffectsQuality", profile.Effects);
        text = SetScalability(text, "sg.FoliageQuality", profile.Foliage);
        text = SetScalability(text, "sg.ShadingQuality", profile.Shading);
        text = SetScalability(text, "sg.LandscapeQuality", profile.Landscape);

        // Optional Ray-Tracing key (appears after first launch on supported GPUs)
        if (profile.RayTracingQuality.HasValue)
            text = SetScalability(text, "sg.RayTracingQuality", profile.RayTracingQuality.Value);

        WriteAll(text);
        SetReadOnly(true);          // prevent the tool from overwriting our settings
    }

    public string GetSettingsSummary(BenchmarkProfile profile)
    {
        return $"""
            Resolution          : {profile.Width}x{profile.Height}
            VSync               : {profile.VSync}
            Frame limit         : {(profile.FrameRateLimit <= 0 ? "Unlimited" : profile.FrameRateLimit.ToString("F0"))}
            ViewDistance        : {profile.ViewDistance}
            AntiAliasing        : {profile.AntiAliasing}
            Shadow              : {profile.Shadow}
            GlobalIllumination  : {profile.GlobalIllumination}
            Reflection          : {profile.Reflection}
            PostProcess         : {profile.PostProcess}
            Texture             : {profile.Texture}
            Effects             : {profile.Effects}
            Foliage             : {profile.Foliage}
            Shading             : {profile.Shading}
            Landscape           : {profile.Landscape}
            RayTracingQuality   : {profile.RayTracingQuality?.ToString() ?? "n/a"}
            """;
    }

    // ---------- low-level INI helpers ----------

    private static string SetKey(string text, string key, string value)
    {
        // Match "Key=anything" (possibly with spaces)
        var pattern = $@"^{Regex.Escape(key)}\s*=.*$";
        var replacement = $"{key}={value}";
        if (Regex.IsMatch(text, pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase))
            return Regex.Replace(text, pattern, replacement, RegexOptions.Multiline | RegexOptions.IgnoreCase);

        // Key not found → append under [/Script/Engine.GameUserSettings]
        const string section = "[/Script/Engine.GameUserSettings]";
        if (text.Contains(section, StringComparison.OrdinalIgnoreCase))
            return text.Replace(section, $"{section}\n{replacement}", StringComparison.OrdinalIgnoreCase);

        return text + $"\n{section}\n{replacement}\n";
    }

    private static string SetScalability(string text, string key, int value)
    {
        // Scalability values are integers 0-4 (or float for ResolutionQuality)
        var strValue = key.Contains("ResolutionQuality", StringComparison.OrdinalIgnoreCase)
            ? value.ToString("F6")
            : value.ToString();

        var pattern = $@"^{Regex.Escape(key)}\s*=.*$";
        var replacement = $"{key}={strValue}";
        if (Regex.IsMatch(text, pattern, RegexOptions.Multiline | RegexOptions.IgnoreCase))
            return Regex.Replace(text, pattern, replacement, RegexOptions.Multiline | RegexOptions.IgnoreCase);

        // Append under [ScalabilityGroups]
        const string section = "[ScalabilityGroups]";
        if (text.Contains(section, StringComparison.OrdinalIgnoreCase))
            return text.Replace(section, $"{section}\n{replacement}", StringComparison.OrdinalIgnoreCase);

        return text + $"\n{section}\n{replacement}\n";
    }
}

/// <summary>
/// Numeric quality levels used by Unreal Engine scalability groups.
/// 0 = Low, 1 = Medium, 2 = High, 3 = Epic, 4 = Cinematic
/// </summary>
public sealed class BenchmarkProfile
{
    public int Width { get; init; } = 1920;
    public int Height { get; init; } = 1080;
    public bool VSync { get; init; } = false;
    public float FrameRateLimit { get; init; } = 0f;   // 0 = unlimited

    public int ResolutionQuality { get; init; } = 100; // percent
    public int ViewDistance { get; init; } = 3;
    public int AntiAliasing { get; init; } = 3;
    public int Shadow { get; init; } = 3;
    public int GlobalIllumination { get; init; } = 3;
    public int Reflection { get; init; } = 3;
    public int PostProcess { get; init; } = 3;
    public int Texture { get; init; } = 3;
    public int Effects { get; init; } = 3;
    public int Foliage { get; init; } = 3;
    public int Shading { get; init; } = 3;
    public int Landscape { get; init; } = 3;
    public int? RayTracingQuality { get; init; }       // null = leave unchanged

    // ---------- Pre-defined profiles ----------

    /// <summary>
    /// CPU-oriented: low resolution + all quality settings at minimum.
    /// Goal: keep GPU almost idle so FPS is limited by CPU / draw calls.
    /// </summary>
    public static BenchmarkProfile CpuTest => new()
    {
        Width = 1280,
        Height = 720,
        VSync = false,
        FrameRateLimit = 0,
        ResolutionQuality = 100,
        ViewDistance = 0,
        AntiAliasing = 0,
        Shadow = 0,
        GlobalIllumination = 0,
        Reflection = 0,
        PostProcess = 0,
        Texture = 0,
        Effects = 0,
        Foliage = 0,
        Shading = 0,
        Landscape = 0,
        RayTracingQuality = 0
    };

    /// <summary>
    /// GPU-oriented: native-ish resolution + maximum quality + RT on.
    /// Goal: maximise shading / RT / geometry load on the GPU.
    /// </summary>
    public static BenchmarkProfile GpuTest(int screenWidth, int screenHeight) => new()
    {
        Width = Math.Max(screenWidth, 1920),
        Height = Math.Max(screenHeight, 1080),
        VSync = false,
        FrameRateLimit = 0,
        ResolutionQuality = 100,
        ViewDistance = 4,
        AntiAliasing = 4,
        Shadow = 4,
        GlobalIllumination = 4,
        Reflection = 4,
        PostProcess = 4,
        Texture = 4,
        Effects = 4,
        Foliage = 4,
        Shading = 4,
        Landscape = 4,
        RayTracingQuality = 3          // Medium/High Full RT when available
    };
}
