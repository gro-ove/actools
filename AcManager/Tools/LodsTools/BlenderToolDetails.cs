using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.LodsTools;
using AcTools.ExtraKn5Utils.LodGenerator;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Serialization;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Helpers.LodGeneratorServices {
    public class BlenderToolDetails : ILodsToolDetails {
        public string Key => "Blender";

        public string Name => "Blender";
        
        public bool NeedsTool => true;

        // Blender's FBX exporter is mature, so we get FBX in and out and skip the FbxConverter step.
        public bool UseFbx => true;

        public bool SplitPriorities => true;
        
        public bool CanWeld => true;

        public IEnumerable<string> DefaultToolLocation {
            get {
                var programFiles = Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files";
                // Newest first - Blender installs are versioned.
                foreach (var version in new[] { "5.0", "4.5", "4.4", "4.3", "4.2", "4.1", "4.0", "3.6", "3.5", "3.4", "3.3" }) {
                    yield return Path.Combine(programFiles, $@"Blender Foundation\Blender {version}\blender.exe");
                    yield return Path.Combine(programFiles.Replace(" (x86)", ""), $@"Blender Foundation\Blender {version}\blender.exe");
                }
                yield return Path.Combine(programFiles, @"Blender Foundation\Blender\blender.exe");
                var programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? @"C:\Program Files (x86)";
                // Steam install
                yield return Path.Combine(programFilesX86, @"Steam\steamapps\common\Blender\blender.exe");
                yield return Path.Combine(programFiles, @"Steam\steamapps\common\Blender\blender.exe");
            }
        }

        public string FindTool(string currentLocation) {
            return FileRelatedDialogs.Open(new OpenDialogParams {
                DirectorySaveKey = "blender",
                InitialDirectory = Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles") ?? @"C:\Program Files", @"Blender Foundation"),
                Filters = {
                    new DialogFilterPiece("Blender", "blender.exe"),
                    DialogFilterPiece.Applications,
                    DialogFilterPiece.AllFiles,
                },
                Title = "Select Blender (blender.exe)",
                DefaultFileName = Path.GetFileName(currentLocation),
            });
        }

        public class LodGeneratorService : ICarLodGeneratorService {
            private readonly string _toolExecutable;
            private readonly IReadOnlyList<ICarLodGeneratorStage> _stages;

            public LodGeneratorService(string toolExecutable, IReadOnlyList<ICarLodGeneratorStage> stages) {
                _toolExecutable = toolExecutable;
                _stages = stages;
            }

            public async Task<string> GenerateLodAsync(string stageId, string inputFile, int inputTriangles, string modelChecksum, bool useUV2,
                    IProgress<double?> progress, CancellationToken cancellationToken) {
                var stage = _stages.GetById(stageId);
                var rulesFilename = stage.ToolConfigurationFilename;
                var scriptData = File.ReadAllText(FilesStorage.Instance.GetContentFile(ContentCategory.CarLodsGeneration,
                        @"ScriptBlender.py").Filename);

                string scriptFile = null;
                try {
                    string cacheKey = null;
                    string filePostfix = "blendered";
                    JToken rules;
                    try {
                        var loaded = File.Exists(rulesFilename) ? JObject.Parse(File.ReadAllText(rulesFilename)) : new JObject();
                        rules = loaded[@"Blender"] ?? new JObject();

                        var rulesData = rules.ToString();
                        // NOTE on stage.ApplyWeldingFix: the Blender pipeline does not perform
                        // vertex welding (Blender preserves custom split normals through the
                        // Decimate modifier; explicitly welding would destroy hard edges that
                        // matter for car panels). The flag is mixed into the cache key purely
                        // for parity with the other tools and so the LOD list label
                        // ("welding"/"no welding") stays meaningful when comparing variants.
                        var combinedChecksum = (modelChecksum + rulesData + inputTriangles + stage.ApplyWeldingFix + useUV2 + scriptData.GetHashCode())
                                .GetChecksum();
                        filePostfix = combinedChecksum.Substring(0, 20);
                        cacheKey = $@"blender:{combinedChecksum}";
                        var existing = CacheStorage.Get<string>(cacheKey);
                        if (existing != null && File.Exists(existing)) {
                            progress?.Report(1d);
                            return existing;
                        }
                    } catch (Exception e) {
                        rules = new JObject();
                        Logging.Warning(e);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(0.01);

                    var workingDir = Path.GetDirectoryName(inputFile) ?? string.Empty;
                    var outputFile = $@"{inputFile.ApartFromLast(@".fbx")}_{filePostfix}.fbx";
                    scriptFile = FileUtils.EnsureUnique($@"{inputFile.ApartFromLast(@".fbx")}_blender.py");
                    FileUtils.TryToDelete(outputFile);
                    File.WriteAllText(scriptFile, scriptData, new UTF8Encoding(false));

                    // Mirror PolygonCruncher's RateLimit/RateScale knobs but expressed as a "keep ratio".
                    // PolygonCruncher uses: removeRate = max(RateLimit, 1 - RateScale * targetTri/inputTri).
                    // Our equivalent keep ratio is: min(1 - RateLimit, RateScale * targetTri/inputTri).
                    var rateLimit = rules.GetDoubleValueOnly("RateLimit", 0.001);
                    var rateScale = rules.GetDoubleValueOnly("RateScale", 1d);
                    var ratio = rateScale * stage.TrianglesCount / Math.Max(1, inputTriangles);
                    var targetPerc = Math.Max(0.001, Math.Min(1d - rateLimit, ratio));
                    Logging.Write($"Blender target keep ratio: {targetPerc * 100:F2}% ({inputTriangles} on input, {stage.TrianglesCount} target)");

                    var blenderArgs = new List<string> {
                        "--background",
                        "--factory-startup",
                        "--python", scriptFile,
                        "--python-exit-code", "1",
                        // Everything after `--` is forwarded to the embedded script's argparse.
                        "--",
                        "--input", inputFile,
                        "--output", outputFile,
                        "--target-perc", targetPerc.ToString("R", CultureInfo.InvariantCulture),
                    };
                    
                    blenderArgs.AddRange(rules["Arguments"]?["Base"]?.ToObject<string[]>() ?? new string[0]);
                    if (useUV2) {
                        blenderArgs.AddRange(rules["Arguments"]?["UV2"]?.ToObject<string[]>() ?? new string[0]);
                    }
                    if (stage.ApplyWeldingFix) {
                        blenderArgs.AddRange(rules["Arguments"]?["Welding"]?.ToObject<string[]>() ?? new string[0]);
                    }
                    
                    string error = null;
                    await RunProcessAsyncHelper.RunAsync(_toolExecutable, blenderArgs, workingDir, true,
                            progress.SubrangeDouble(0.01, 0.99), cancellationToken, err => error = err, err => {
                                // Blender prints a lot of irrelevant chatter; keep the last error-tagged line if there is one.
                                var lines = err.Split('\n');
                                for (var i = lines.Length - 1; i >= 0; i--) {
                                    var trimmed = lines[i].Trim();
                                    if (trimmed.Length > 0 && (trimmed.StartsWith("Blender") || trimmed.Contains("Error")
                                            || trimmed.Contains("Exception"))) {
                                        return trimmed;
                                    }
                                }
                                return err;
                            }).ConfigureAwait(false);

                    cancellationToken.ThrowIfCancellationRequested();
                    if (!File.Exists(outputFile)) {
                        throw new Exception(error ?? "Blender hasn’t produced an output FBX");
                    }

                    if (cacheKey != null) {
                        CacheStorage.Set(cacheKey, outputFile);
                    }
                    progress?.Report(1d);
                    return outputFile;
                } finally {
                    if (!stage.KeepTemporaryFiles) {
                        if (scriptFile != null) FileUtils.TryToDelete(scriptFile);
                    }
                }
            }
        }

        public ICarLodGeneratorService Create(string toolExecutable, IReadOnlyList<ICarLodGeneratorStage> stages) {
            return new LodGeneratorService(toolExecutable, stages);
        }
    }
}
