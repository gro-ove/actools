using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.LodsTools;
using AcTools.ExtraKn5Utils.LodGenerator;
using AcTools.Kn5File;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Serialization;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Helpers.LodGeneratorServices {
    public class MeshOptimizerToolDetails : ILodsToolDetails {
        public string Key => "MeshOptimizer";

        public string Name => "meshoptimizer";

        // PyMeshLab cannot read FBX; this helper uses the same COLLADA round trip + FbxConverter path.
        public bool UseFbx => false;

        public bool SplitPriorities => true;
        
        public bool NeedsTool => false;

        public IEnumerable<string> DefaultToolLocation => null;

        public string FindTool(string currentLocation) {
            return null;
        }

        private static async Task RunProcessAsync(string filename, [Localizable(false)] IEnumerable<string> args, string workingDirectory,
                bool checkErrorCode, IProgress<double?> progress, CancellationToken cancellationToken, Action<string> errorCallback) {
            var process = ProcessExtension.Start(filename, args, new ProcessStartInfo {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory ?? string.Empty,
            });
            try {
                ChildProcessTracker.AddProcess(process);
                cancellationToken.ThrowIfCancellationRequested();

                var errorData = new StringBuilder();
                process.ErrorDataReceived += (sender, eventArgs) => {
                    if (eventArgs.Data == null) return;
                    if (errorData.Length > 0) errorData.Append('\n');
                    errorData.Append(eventArgs.Data);
                };
                process.BeginErrorReadLine();

                process.OutputDataReceived += (sender, eventArgs) => {
                    if (eventArgs.Data == null || progress == null) return;
                    var v = eventArgs.Data.As<double?>();
                    if (v.HasValue) progress.Report(v.Value / 100d);
                };
                process.BeginOutputReadLine();

                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                if (errorCallback != null && errorData.Length > 0) {
                    errorCallback.Invoke(errorData.ToString().Trim());
                }
                if (checkErrorCode && process.ExitCode != 0) {
                    var errorMessage = errorData.ToString().Trim();
                    if (string.IsNullOrEmpty(errorMessage)) {
                        errorMessage = $@"Failed to run meshoptimizer LOD tool: {process.ExitCode}";
                    } else {
                        var separator = errorMessage.LastIndexOf(@": ", StringComparison.Ordinal);
                        if (separator != -1) {
                            errorMessage = errorMessage.Substring(separator + 2);
                        }
                    }
                    throw new Exception(errorMessage);
                }
            } finally {
                try {
                    if (!process.HasExitedSafe()) {
                        process.Kill();
                    }
                } catch (Exception killEx) {
                    Logging.Debug($"Failed to kill meshoptimizer helper process: {killEx.Message}");
                }
                process.Dispose();
            }
        }

        public class LodGeneratorService : ICarLodGeneratorService {
            private static SingleTimeInitHelper _initHelper = new SingleTimeInitHelper();
            
            private readonly IReadOnlyList<ICarLodGeneratorStage> _stages;

            public LodGeneratorService(IReadOnlyList<ICarLodGeneratorStage> stages) {
                _stages = stages;
            }

            public async Task<string> GenerateLodAsync(string stageId, string inputFile, int inputTriangles, string modelChecksum, bool useUV2,
                    IProgress<double?> progress, CancellationToken cancellationToken) {
                var stage = _stages.GetById(stageId);
                var rulesFilename = stage.ToolConfigurationFilename;

                var toolFilename = FilesStorage.Instance.GetFilename("Temporary", "meshoptimizerlod.exe");
                toolFilename = "C:/Development/CppSnippets/.output/meshoptimizerlod.exe";
                await _initHelper.DoAsync(async () => {
                    var data = await CmApiProvider.GetStaticDataAsync("meshoptimizerlod", TimeSpan.FromDays(1), cancellation: cancellationToken);
                    if (cancellationToken.IsCancellationRequested) return;
                    if (data == null) throw new InformativeException("Failed to load the LOD generation tool");
                    await Task.Run(() => {
                        using (var archive = ZipFile.OpenRead(data.Item1)) {
                            foreach (var file in archive.Entries) {
                                if (file.Name == "meshoptimizerlod.exe") {
                                    FileUtils.EnsureFileDirectoryExists(toolFilename);
                                    file.ExtractToFile(toolFilename, true);
                                    return;
                                }
                            }
                        }
                        throw new InformativeException("Package with LOD generation tool is damaged");
                    });
                }, cancellationToken).ConfigureAwait(false);
                if (!File.Exists(toolFilename)) {
                    throw new InformativeException("Failed to find the LOD generation tool");
                }

                string intermediateFile = null;
                try {
                    string cacheKey = null;
                    string filePostfix = "meshoptimized";
                    JToken rules;
                    try {
                        var loaded = File.Exists(rulesFilename) ? JObject.Parse(File.ReadAllText(rulesFilename)) : new JObject();
                        rules = loaded[@"MeshOptimizer"] ?? new JObject();

                        var rulesData = rules.ToString();
                        var combinedChecksum = (modelChecksum + rulesData + inputTriangles + stage.ApplyWeldingFix + useUV2).GetChecksum();
                        filePostfix = combinedChecksum.Substring(0, 20);
                        cacheKey = $@"meshOptimizer:{combinedChecksum}";
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
                    intermediateFile = $@"{inputFile.ApartFromLast(@".dae")}_{filePostfix}.dae";
                    FileUtils.TryToDelete(intermediateFile);

                    // Mirror PolygonCruncher's RateLimit/RateScale knobs but expressed as a "keep ratio".
                    var rateLimit = rules.GetDoubleValueOnly("RateLimit", 0.001);
                    var rateScale = rules.GetDoubleValueOnly("RateScale", 1d);
                    var ratio = rateScale * stage.TrianglesCount / Math.Max(1, inputTriangles);
                    var targetPerc = Math.Max(0.001, Math.Min(1d - rateLimit, ratio));
                    Logging.Write($"meshoptimizer target keep ratio: {targetPerc * 100:F2}% ({inputTriangles} on input, {stage.TrianglesCount} target)");

                    var args = new List<string> {
                        "--input", Path.GetFileName(inputFile),
                        "--output", Path.GetFileName(intermediateFile),
                        "--target-perc", targetPerc.ToString("R", CultureInfo.InvariantCulture),
                    };

                    args.AddRange(rules["Arguments"]?["Base"]?.ToObject<string[]>() ?? new string[0]);
                    if (useUV2) {
                        args.AddRange(rules["Arguments"]?["UV2"]?.ToObject<string[]>() ?? new string[0]);
                    }
                    if (stage.ApplyWeldingFix) {
                        args.AddRange(rules["Arguments"]?["Welding"]?.ToObject<string[]>() ?? new string[0]);
                    }

                    string error = null;
                    await RunProcessAsync(toolFilename, args, workingDir, true,
                            progress.SubrangeDouble(0.01, 0.9), cancellationToken, err => error = err).ConfigureAwait(false);
                    
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!File.Exists(intermediateFile)) {
                        throw new Exception(error ?? "meshoptimizer hasn’t created an output file");
                    }

                    var outputFile = $@"{inputFile.ApartFromLast(@".dae")}_{filePostfix}.fbx";
                    FileUtils.TryToDelete(outputFile);

                    string fbxError = null;
                    await RunProcessAsync(Kn5.FbxConverterLocation,
                            new[] { intermediateFile, outputFile, "/sffCOLLADA", "/dffFBX", "/f201300" },
                            workingDir, true, progress.SubrangeDouble(0.9, 0.99), cancellationToken,
                            err => fbxError = err).ConfigureAwait(false);

                    if (!File.Exists(outputFile)) {
                        throw new Exception(fbxError ?? "FbxConverter hasn’t produced an FBX from meshoptimizer’s output");
                    }

                    if (cacheKey != null) {
                        CacheStorage.Set(cacheKey, outputFile);
                    }
                    progress?.Report(1d);
                    return outputFile;
                } finally {
                    if (!stage.KeepTemporaryFiles) {
                        if (intermediateFile != null) FileUtils.TryToDelete(intermediateFile);
                    }
                }
            }
        }

        public ICarLodGeneratorService Create(string toolExecutable, IReadOnlyList<ICarLodGeneratorStage> stages) {
            return new LodGeneratorService(stages);
        }
    }
}
