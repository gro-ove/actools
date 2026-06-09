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
using AcTools.Kn5File;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Serialization;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Helpers.LodGeneratorServices {
    public class MeshLabToolDetails : ILodsToolDetails {
        public string Key => "MeshLab";

        public string Name => "MeshLab";

        // PyMeshLab can read/write COLLADA but not FBX, so the upstream pipeline gives us .dae.
        public bool UseFbx => false;

        public bool SplitPriorities => true;
        
        public bool NeedsTool => true;
        
        public bool CanWeld => true;

        public IEnumerable<string> DefaultToolLocation {
            get {
                var localPrograms = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        @"Programs\Python");
                yield return Path.Combine(localPrograms, @"Python313\python.exe");
                yield return Path.Combine(localPrograms, @"Python312\python.exe");
                yield return Path.Combine(localPrograms, @"Python311\python.exe");
                yield return Path.Combine(localPrograms, @"Python310\python.exe");
                yield return Path.Combine(localPrograms, @"Python39\python.exe");
            }
        }

        public string FindTool(string currentLocation) {
            return FileRelatedDialogs.Open(new OpenDialogParams {
                DirectorySaveKey = "python",
                InitialDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        @"Programs\Python"),
                Filters = {
                    new DialogFilterPiece("Python", "python.exe"),
                    DialogFilterPiece.Applications,
                    DialogFilterPiece.AllFiles,
                },
                Title = "Select Python with PyMeshLab installed (pip install pymeshlab)",
                DefaultFileName = Path.GetFileName(currentLocation),
            });
        }

        public class LodGeneratorService : ICarLodGeneratorService {
            private static SingleTimeInitHelper _initHelper = new SingleTimeInitHelper();
            
            private readonly string _toolExecutable;
            private readonly IReadOnlyList<ICarLodGeneratorStage> _stages;
            
            public LodGeneratorService(string toolExecutable, IReadOnlyList<ICarLodGeneratorStage> stages) {
                _toolExecutable = toolExecutable;
                _stages = stages;
            }

            // Probe whether pymeshlab is importable from the configured Python interpreter, and if it
            // isn't, run pip install on the user's behalf so they only need to install Python itself.
            // Result is cached for the lifetime of the service; the semaphore makes sure two concurrent
            // first calls (e.g. parallel LOD stages) don't both kick off a pip install.
            private Task EnsurePyMeshLabReadyAsync(IProgress<double?> progress, CancellationToken cancel) {
               return _initHelper.DoAsync(async () => {
                    if (await IsPyMeshLabImportableAsync(cancel).ConfigureAwait(false)) {
                        return;
                    }

                    Logging.Write($"PyMeshLab is missing for \"{_toolExecutable}\"; installing via pip...");

                    string pipError = null;
                    try {
                        await RunProcessAsyncHelper.RunAsync(_toolExecutable,
                                new[] { "-m", "pip", "install", "pymeshlab" },
                                Path.GetTempPath(), true, progress, cancel,
                                err => pipError = err).ConfigureAwait(false);
                    } catch (OperationCanceledException) {
                        throw;
                    } catch (Exception pipEx) {
                        throw new Exception(
                                $"Failed to install PyMeshLab via pip. You can install it manually:\n"
                                        + $"\"{_toolExecutable}\" -m pip install pymeshlab\n\n"
                                        + pipEx.Message, pipEx);
                    }

                    if (!await IsPyMeshLabImportableAsync(cancel).ConfigureAwait(false)) {
                        throw new Exception(
                                $"PyMeshLab pip install finished but \"import pymeshlab\" still fails for "
                                        + $"\"{_toolExecutable}\"."
                                        + (string.IsNullOrEmpty(pipError) ? string.Empty : "\nLast pip output:\n" + pipError));
                    }

                    Logging.Write("PyMeshLab installed successfully");
                }, cancel);
            }

            private async Task<bool> IsPyMeshLabImportableAsync(CancellationToken cancel) {
                try {
                    var process = ProcessExtension.Start(_toolExecutable,
                            new[] { "-c", "import pymeshlab" },
                            new ProcessStartInfo {
                                UseShellExecute = false,
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                CreateNoWindow = true,
                            });
                    try {
                        await process.WaitForExitAsync(cancel).ConfigureAwait(false);
                        return process.ExitCode == 0;
                    } finally {
                        try {
                            if (!process.HasExitedSafe()) process.Kill();
                        } catch (Exception killEx) {
                            Logging.Debug($"Failed to kill PyMeshLab probe: {killEx.Message}");
                        }
                        process.Dispose();
                    }
                } catch (OperationCanceledException) {
                    throw;
                } catch (Exception e) {
                    Logging.Debug($"PyMeshLab probe failed for \"{_toolExecutable}\": {e.Message}");
                    return false;
                }
            }

            public async Task<string> GenerateLodAsync(string stageId, string inputFile, int inputTriangles, string modelChecksum, bool useUV2,
                    IProgress<double?> progress, CancellationToken cancellationToken) {
                var stage = _stages.GetById(stageId);
                var rulesFilename = stage.ToolConfigurationFilename;
                var scriptData = File.ReadAllText(FilesStorage.Instance.GetContentFile(ContentCategory.CarLodsGeneration,
                        @"ScriptMeshLab.py").Filename);

                string scriptFile = null;
                string intermediateFile = null;
                try {
                    string cacheKey = null;
                    string filePostfix = "meshlabed";
                    JToken rules;
                    try {
                        var loaded = File.Exists(rulesFilename) ? JObject.Parse(File.ReadAllText(rulesFilename)) : new JObject();
                        rules = loaded[@"MeshLab"] ?? new JObject();

                        var rulesData = rules.ToString();
                        var combinedChecksum = (modelChecksum + rulesData + inputTriangles + stage.ApplyWeldingFix + useUV2 + scriptData.GetHashCode()).GetChecksum();
                        filePostfix = combinedChecksum.Substring(0, 20);
                        cacheKey = $@"meshLab:{combinedChecksum}";
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

                    // Make sure the configured Python interpreter actually has pymeshlab; otherwise
                    // pip install it now so the user only needs to provide a vanilla Python install.
                    await EnsurePyMeshLabReadyAsync(progress, cancellationToken).ConfigureAwait(false);

                    var workingDir = Path.GetDirectoryName(inputFile) ?? string.Empty;
                    intermediateFile = $@"{inputFile.ApartFromLast(@".dae")}_{filePostfix}.dae";
                    scriptFile = FileUtils.EnsureUnique($@"{inputFile.ApartFromLast(@".dae")}_meshlab.py");
                    FileUtils.TryToDelete(intermediateFile);
                    File.WriteAllText(scriptFile, scriptData, new UTF8Encoding(false));

                    // Mirror PolygonCruncher's RateLimit/RateScale knobs but expressed as a "keep ratio".
                    // PolygonCruncher uses: removeRate = max(RateLimit, 1 - RateScale * targetTri/inputTri).
                    // Our equivalent keep ratio is: min(1 - RateLimit, RateScale * targetTri/inputTri).
                    var rateLimit = rules.GetDoubleValueOnly("RateLimit", 0.001);
                    var rateScale = rules.GetDoubleValueOnly("RateScale", 1d);
                    var ratio = rateScale * stage.TrianglesCount / Math.Max(1, inputTriangles);
                    var targetPerc = Math.Max(0.001, Math.Min(1d - rateLimit, ratio));
                    Logging.Write($"MeshLab target keep ratio: {targetPerc * 100:F2}% ({inputTriangles} on input, {stage.TrianglesCount} target)");

                    var args = new List<string> {
                        Path.GetFileName(scriptFile),
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
                    
                    if (stage.KeepTemporaryFiles) args.Add("--keep-temp");

                    string error = null;
                    await RunProcessAsyncHelper.RunAsync(_toolExecutable, args, workingDir, true,
                            progress.SubrangeDouble(0.01, 0.9), cancellationToken, err => error = err).ConfigureAwait(false);

                    cancellationToken.ThrowIfCancellationRequested();
                    if (!File.Exists(intermediateFile)) {
                        throw new Exception(error ?? "MeshLab hasn’t created an output file");
                    }

                    var outputFile = $@"{inputFile.ApartFromLast(@".dae")}_{filePostfix}.fbx";
                    FileUtils.TryToDelete(outputFile);

                    string fbxError = null;
                    await RunProcessAsyncHelper.RunAsync(Kn5.FbxConverterLocation,
                            new[] { intermediateFile, outputFile, "/sffCOLLADA", "/dffFBX", "/f201300" },
                            workingDir, true, progress.SubrangeDouble(0.9, 0.99), cancellationToken,
                            err => fbxError = err).ConfigureAwait(false);

                    if (!File.Exists(outputFile)) {
                        throw new Exception(fbxError ?? "FbxConverter hasn’t produced an FBX from MeshLab’s output");
                    }

                    if (cacheKey != null) {
                        CacheStorage.Set(cacheKey, outputFile);
                    }
                    progress?.Report(1d);
                    return outputFile;
                } finally {
                    if (!stage.KeepTemporaryFiles) {
                        if (scriptFile != null) FileUtils.TryToDelete(scriptFile);
                        if (intermediateFile != null) FileUtils.TryToDelete(intermediateFile);
                    }
                }
            }
        }

        public ICarLodGeneratorService Create(string toolExecutable, IReadOnlyList<ICarLodGeneratorStage> stages) {
            return new LodGeneratorService(toolExecutable, stages);
        }
    }
}
