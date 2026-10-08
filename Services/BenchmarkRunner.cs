using System.Diagnostics;
using WukongBenchmarkAutomator.Models;

namespace WukongBenchmarkAutomator.Services;

public sealed class BenchmarkRunner
{
    private readonly string _installDir;
    private readonly IniSettingsManager _ini;
    private readonly TimeSpan _timeout;

    public BenchmarkRunner(string installDir, TimeSpan? timeout = null)
    {
        _installDir = installDir;
        _ini = new IniSettingsManager(installDir);
        _timeout = timeout ?? TimeSpan.FromMinutes(8);   // shader compile + run
    }

    public IniSettingsManager Ini => _ini;

    /// <summary>
    /// Applies the given profile, launches the benchmark executable and waits
    /// for it to exit (or times out). Returns a result object that still needs
    /// the numeric values filled by ResultsParser.
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
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                WorkingDirectory = Path.GetDirectoryName(exe) ?? _installDir,
                UseShellExecute = true,          // needed for Steam overlay / proper working dir
                CreateNoWindow = false
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                result.Success = false;
                result.Error = "Process.Start returned null";
                return result;
            }

            // Wait with timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(_timeout);

            try
            {
                await process.WaitForExitAsync(cts.Token);
                Console.WriteLine($"[{label}] Process exited with code {process.ExitCode}");
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                Console.WriteLine($"[{label}] Timeout reached – killing process");
                try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
                result.Success = false;
                result.Error = $"Benchmark did not finish within {_timeout.TotalMinutes:F0} minutes";
                return result;
            }

            // Give the tool a moment to flush any log files
            await Task.Delay(1500, ct);

            result.Success = true;
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
            // Always restore write access so the user can still change settings manually
            _ini.SetReadOnly(false);
        }
    }

    private string? FindExecutable()
    {
        // Prefer the official launcher name from SteamDB
        var candidates = new[]
        {
            Path.Combine(_installDir, "b1_benchmark.exe"),
            Path.Combine(_installDir, "b1", "Binaries", "Win64", "b1-Win64-Shipping.exe"),
            Path.Combine(_installDir, "b1", "Binaries", "Win64", "b1_benchmark.exe")
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}
