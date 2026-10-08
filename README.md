# Black Myth: Wukong Benchmark Automator

.NET 8 console tool that **automatically** runs the free *Black Myth: Wukong Benchmark Tool* (Steam AppID **3132990**) twice with different graphics profiles and produces a readable report.

## What it does

1. Locates the Benchmark Tool installation (Steam registry / library folders / `WUKONG_BENCHMARK_PATH` env var).
2. Collects system information (CPU, GPU, RAM, OS).
3. **CPU-test** – writes a low-resolution / lowest-quality profile into `GameUserSettings.ini`, launches the benchmark, waits for completion.
4. **GPU-test** – writes a high-resolution / maximum-quality (+ Ray Tracing) profile, launches again, waits.
5. Tries to extract FPS numbers from log files (OCR stub is ready for extension).
6. Prints a full report to the console **and** saves a timestamped `.txt` file.

Everything runs unattended after you start the program.

---

## Prerequisites

- Windows 10 / 11 (x64)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (for building) or just the runtime (for running a published binary)
- *Black Myth: Wukong Benchmark Tool* installed via Steam (free)

---

## Build & Run

```bash
git clone <your-repo-url>
cd WukongBenchmarkAutomator

# restore & build
dotnet restore
dotnet build -c Release

# run (from the project folder)
dotnet run -c Release -- --width 2560 --height 1440
```

Or publish a single-file executable:

```bash
dotnet publish -c Release -r win-x64 --self-contained false -o ./publish
./publish/WukongBenchmarkAutomator.exe --width 1920 --height 1080
```

### Optional arguments

| Argument       | Meaning                          | Default |
|----------------|----------------------------------|---------|
| `--width N`    | Horizontal resolution for GPU-test | 1920  |
| `--height N`   | Vertical resolution for GPU-test   | 1080  |

### Environment variable

```
WUKONG_BENCHMARK_PATH=D:\SteamLibrary\steamapps\common\Black Myth Wukong Benchmark Tool
```

Useful when the tool is installed in a non-standard library folder.

---

## Chosen settings & why

### CPU-test (isolate the processor)

| Setting              | Value     | Reason |
|----------------------|-----------|--------|
| Resolution           | 1280×720  | Low pixel count → GPU stays almost idle |
| All scalability groups | 0 (Low) | Minimal geometry, shadows, effects, foliage… |
| Ray Tracing          | 0 (Off)   | Removes the heaviest GPU feature |
| VSync / Frame limit  | Off / Unlimited | Let the CPU push as many frames as it can |

With these settings the frame rate is dominated by CPU work (draw-call submission, game logic, Unreal tick) rather than by the GPU.

### GPU-test (maximise GPU load)

| Setting              | Value              | Reason |
|----------------------|--------------------|--------|
| Resolution           | native / user-supplied (default 1920×1080) | High pixel load |
| All scalability groups | 4 (Cinematic)   | Highest quality for shadows, GI, reflections, foliage, effects… |
| Ray Tracing          | 3 (Medium/High)    | Enables Full Ray Tracing when the hardware supports it |
| VSync / Frame limit  | Off / Unlimited    | Measure true GPU throughput |

This profile forces the GPU to do as much shading, ray-tracing and post-processing work as possible, making the result primarily GPU-bound.

---

## How settings are applied

The tool edits:

```
<install>\b1\Saved\Config\Windows\GameUserSettings.ini
```

It writes the `[ScalabilityGroups]` section and resolution keys, then sets the file **read-only** so the Benchmark Tool cannot overwrite the values on start-up. After each run the read-only flag is cleared again.

---

## Results extraction (current status)

The official Benchmark Tool only shows numbers on-screen.  
`ResultsParser` currently:

1. Scans the newest `*.log` files under `b1\Saved\Logs` for typical FPS strings (English + Chinese).
2. If nothing is found, prints a clear note that OCR can be plugged in.

To add real OCR later:

```xml
<PackageReference Include="Tesseract" Version="5.2.0" />
```

Then capture the results window (or the whole desktop) after the process exits and feed the bitmap into Tesseract → `ResultsParser.ParseFpsFromText(...)`.

---

## Project structure

```
WukongBenchmarkAutomator/
├── WukongBenchmarkAutomator.csproj
├── Program.cs                     ← entry point & report
├── Models/
│   ├── SystemInfo.cs
│   └── BenchmarkResult.cs
├── Services/
│   ├── SteamLocator.cs            ← find install dir
│   ├── IniSettingsManager.cs      ← read/write GameUserSettings.ini + profiles
│   ├── SystemInfoCollector.cs     ← WMI CPU/GPU/RAM
│   ├── BenchmarkRunner.cs         ← launch + wait
│   └── ResultsParser.cs           ← log / OCR stub
└── README.md
```

---

## License

MIT – do whatever you want with the code.

---

## Disclaimer

- The first launch of the Benchmark Tool may spend several minutes compiling shaders; the default timeout is 8 minutes.
- Results obtained from the Benchmark Tool are indicative only and may differ from the full game.
- This automator is an independent community tool and is not affiliated with Game Science or Steam.
