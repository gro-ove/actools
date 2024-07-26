using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
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
    public class SimplygonToolDetails : ILodsToolDetails {
        public string Key => "Simplygon";

        public bool UseFbx => true;

        public bool SplitPriorities => false;

        public IEnumerable<string> DefaultToolLocation => new[] { @"C:\Program Files\Simplygon\10\SimplygonBatch.exe" };

        public string FindTool(string currentLocation) {
            return FileRelatedDialogs.Open(new OpenDialogParams {
                DirectorySaveKey = "simplygon",
                InitialDirectory = Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles")?.Replace(@" (x86)", "")
                        ?? @"C:\Program Files", @"Simplygon\9"),
                Filters = {
                    new DialogFilterPiece("Simplygon Batch Tool", "SimplygonBatch.exe"),
                    DialogFilterPiece.Applications,
                    DialogFilterPiece.AllFiles,
                },
                Title = "Select Simplygon Batch tool",
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

            private static async Task RunProcessAsync(string filename, [Localizable(false)] IEnumerable<string> args, bool checkErrorCode,
                    IProgress<double?> progress, CancellationToken cancellationToken, Action<string> errorCallback) {
                var process = ProcessExtension.Start(filename, args, new ProcessStartInfo {
                    UseShellExecute = false,
                    RedirectStandardOutput = progress != null,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                });
                try {
                    ChildProcessTracker.AddProcess(process);
                    cancellationToken.ThrowIfCancellationRequested();

                    var errorData = new StringBuilder();
                    process.ErrorDataReceived += (sender, eventArgs) => errorData.Append(eventArgs.Data);
                    process.BeginErrorReadLine();

                    if (progress != null) {
                        process.OutputDataReceived += (sender, eventArgs) => progress.Report(eventArgs.Data.As<double?>() / 100d);
                        process.BeginOutputReadLine();
                    }

                    await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                    if (errorCallback != null && errorData.Length > 0) {
                        errorCallback.Invoke(errorData.ToString().Trim());
                    }
                    if (checkErrorCode && process.ExitCode != 0) {
                        var errorMessage = errorData.ToString().Trim();
                        if (string.IsNullOrEmpty(errorMessage)) {
                            errorMessage = $@"Failed to run process: {process.ExitCode}";
                        } else {
                            var separator = errorMessage.LastIndexOf(@": ", StringComparison.Ordinal);
                            if (separator != -1) {
                                errorMessage = errorMessage.Substring(separator + 2);
                            }
                        }
                        throw new Exception(errorMessage);
                    }
                } finally {
                    if (!process.HasExitedSafe()) {
                        process.Kill();
                    }
                    process.Dispose();
                }
            }

            public async Task<string> GenerateLodAsync(string stageId, string inputFile, int inputTriangles, string modelChecksum, bool useUV2,
                    IProgress<double?> progress, CancellationToken cancellationToken) {
                var stage = _stages.GetById(stageId);
                var rulesFilename = stage.ToolConfigurationFilename;
                string intermediateFilename = null;

                try {
                    string cacheKey = null;
                    try {
                        var rules = JObject.Parse(File.ReadAllText(rulesFilename))[@"Simplygon"];
                        rules[@"Settings"][@"ReductionProcessor"][@"ReductionSettings"][@"ReductionTargetTriangleCount"] = stage.TrianglesCount;
                        if (!useUV2) rules[@"Settings"][@"ReductionProcessor"][@"ReductionSettings"][@"VertexColorImportance"] = 0f;
                        rules[@"Settings"][@"ReductionProcessor"][@"RepairSettings"][@"UseWelding"] = stage.ApplyWeldingFix;
                        rules[@"Settings"][@"ReductionProcessor"][@"RepairSettings"][@"UseTJunctionRemover"] = stage.ApplyWeldingFix;

                        var rulesData = rules.ToString();
                        var combinedChecksum = (modelChecksum + rulesData).GetChecksum();
                        cacheKey = $@"simplygon:{combinedChecksum}";
                        var existing = CacheStorage.Get<string>(cacheKey);
                        if (existing != null && File.Exists(existing)) {
                            progress?.Report(1d);
                            return existing;
                        }

                        var newRulesFilename = FileUtils.EnsureUnique($@"{inputFile.ApartFromLast(@".fbx")}_rules.json");
                        File.WriteAllText(newRulesFilename, rulesData);
                        rulesFilename = newRulesFilename;
                    } catch (Exception e) {
                        Logging.Warning(e);
                    }

                    // Simplygon can’t parse FBX generated from COLLADA by FbxConverter. But it can parse FBX generated from FBX from COLLADA! So, let’s
                    // convert it once more:
                    intermediateFilename = FileUtils.EnsureUnique($@"{inputFile.ApartFromLast(@".fbx")}_fixed.fbx");
                    await RunProcessAsync(Kn5.FbxConverterLocation, new[] { inputFile, intermediateFilename, "/sffFBX", "/dffFBX", "/f201300" },
                            true, null, cancellationToken, null).ConfigureAwait(false);

                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(0.01);

                    var outputFile = FileUtils.EnsureUnique($@"{inputFile.ApartFromLast(@".fbx")}_simplygon.fbx");
                    string errorMessage = null;
                    await RunProcessAsync(_toolExecutable, new[] {
                        "-Progress", rulesFilename, intermediateFilename, outputFile
                    }, false, progress.SubrangeDouble(0.01, 1d), cancellationToken, err => errorMessage = err)
                            .ConfigureAwait(false);
                    if (cacheKey != null) {
                        CacheStorage.Set(cacheKey, outputFile);
                    }
                    if (!File.Exists(outputFile)) {
                        throw new Exception(errorMessage ?? "Simplygon hasn’t created an output file");
                    }
                    return outputFile;
                } finally {
                    if (!stage.KeepTemporaryFiles) {
                        if (!FileUtils.ArePathsEqual(rulesFilename, stage.ToolConfigurationFilename)) FileUtils.TryToDelete(rulesFilename);
                        if (intermediateFilename != null) FileUtils.TryToDelete(intermediateFilename);
                    }
                }
            }
        }

        public ICarLodGeneratorService Create(string toolExecutable, IReadOnlyList<ICarLodGeneratorStage> stages) {
            return new LodGeneratorService(toolExecutable, stages);
        }
    }
}