using System.Diagnostics;
using WukongBenchmarkAutomator.Models;

namespace WukongBenchmarkAutomator.Services;

public sealed class BenchmarkRunner
{
    private readonly string _installDir;
    private readonly IniSettingsManager _ini;

    public BenchmarkRunner(string installDir)
    {
        _installDir = installDir;
        _ini = new IniSettingsManager(installDir);
    }

    public IniSettingsManager Ini => _ini;

    /// <summary>
    /// Applies profile, launches the tool, then waits until the user
    /// presses Enter (after the results screen is visible).
    /// </summary>
    public async Task<BenchmarkResult> RunAsync(string label, BenchmarkProfile profile, CancellationToken ct = default)
    {
        var result = new BenchmarkResult
        {
            Label = label,
            SettingsSummary = _ini.GetSettingsSummary(profile)
        };

        try
        {
            Console.WriteLine($"[{label}] Writing settings…");
            _ini.ApplyProfile(profile);

            var exe = FindExecutable();
            if (exe is null)
            {
                result.Success = false;
                result.Error = "Could not locate b1_benchmark.exe / b1-Win64-Shipping.exe";
                return result;
            }

            Console.WriteLine($"[{label}] Launching: {exe}");
            Console.WriteLine($"[{label}] In the Benchmark Tool:");
            Console.WriteLine($"         1. Confirm language / brightness if asked");
            Console.WriteLine($"         2. Click / select  Benchmark");
            Console.WriteLine($"         3. Wait until the results screen appears");
            Console.WriteLine($"         4. Come back here and press  Enter");
            Console.WriteLine();

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                WorkingDirectory = Path.GetDirectoryName(exe) ?? _installDir,
                UseShellExecute = true,
                CreateNoWindow = false
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                result.Success = false;
                result.Error = "Process.Start returned null";
                return result;
            }

            // Wait for user to press Enter (results are on screen)
            await WaitForUserEnterAsync(ct);
            Console.WriteLine($"[{label}] User confirmed – collecting numbers next.");

            result.Success = true;
            return result;
        }
        catch (OperationCanceledException)
        {
            result.Success = false;
            result.Error = "Cancelled";
            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
            return result;
        }
        finally
        {
            _ini.SetReadOnly(false);
        }
    }

    private static async Task WaitForUserEnterAsync(CancellationToken ct)
    {
        await Task.Run(() =>
        {
            try
            {
                Console.WriteLine(">>> Press ENTER when the benchmark results screen is visible…");
                Console.ReadLine();
            }
            catch (Exception) when (ct.IsCancellationRequested) { }
        }, ct);
    }

    private string? FindExecutable()
    {
        var candidates = new[]
        {
            Path.Combine(_installDir, "b1_benchmark.exe"),
            Path.Combine(_installDir, "b1", "Binaries", "Win64", "b1-Win64-Shipping.exe"),
            Path.Combine(_installDir, "b1", "Binaries", "Win64", "b1_benchmark.exe")
        };
        return candidates.FirstOrDefault(File.Exists);
    }
}
