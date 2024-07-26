using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

        public bool UseFbx => false;

        public bool SplitPriorities => true;

        public IEnumerable<string> DefaultToolLocation
            => new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs\\Python\\Python310") };

        public string FindTool(string currentLocation) {
            return FileRelatedDialogs.Open(new OpenDialogParams {
                DirectorySaveKey = "python",
                InitialDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs\\Python\\Python310"),
                Filters = {
                    new DialogFilterPiece("Python 3", "python.exe"),
                    DialogFilterPiece.Applications,
                    DialogFilterPiece.AllFiles,
                },
                Title = "Select Python installation (make sure to have PyMeshLab installed)",
                DefaultFileName = Path.GetFileName(currentLocation),
            });
        }

        private static async Task RunProcessAsync(string filename, [Localizable(false)] IEnumerable<string> args, string workingDirectory, bool checkErrorCode,
                IProgress<double?> progress, CancellationToken cancellationToken, Action<string> errorCallback) {
            var process = ProcessExtension.Start(filename, args, new ProcessStartInfo {
                UseShellExecute = false,
                RedirectStandardOutput = progress != null,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = workingDirectory
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

        public class LodGeneratorService : ICarLodGeneratorService {
            private readonly string _toolExecutable;
            private readonly IReadOnlyList<ICarLodGeneratorStage> _stages;

            public LodGeneratorService(string cruncherExecutable, IReadOnlyList<ICarLodGeneratorStage> stages) {
                _toolExecutable = cruncherExecutable;
                _stages = stages;
            }

            public async Task<string> GenerateLodAsync(string stageId, string inputFile, int inputTriangles, string modelChecksum, bool useUV2,
                    IProgress<double?> progress, CancellationToken cancellationToken) {
                var stage = _stages.GetById(stageId);
                var rulesFilename = stage.ToolConfigurationFilename;

                try {
                    string cacheKey = null;
                    string filePostfix = "meshlabed";
                    JToken rules;
                    try {
                        throw new Exception();
                        /*rules = JObject.Parse(File.ReadAllText(rulesFilename))[@"PolygonCruncher"];

                        var rulesData = rules.ToString();
                        var combinedChecksum = (modelChecksum + rulesData + inputTriangles + stage.ApplyWeldingFix + useUV2).GetChecksum();
                        filePostfix = combinedChecksum.Substring(0, 20);
                        cacheKey = $@"polygonCruncher:{combinedChecksum}";
                        var existing = CacheStorage.Get<string>(cacheKey);
                        if (existing != null && File.Exists(existing)) {
                            progress?.Report(1d);
                            return existing;
                        }*/
                    } catch (Exception e) {
                        rules = new JObject();
                        Logging.Warning(e);
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(0.01);

                    var itermediateFile = $@"{inputFile.ApartFromLast(@".dae")}_{filePostfix}.dae";
                    var scriptFile = FileUtils.EnsureUnique($@"{inputFile.ApartFromLast(@".dae")}_run.py");
                    var logFile = $@"{inputFile.ApartFromLast(@".fbx")}_meshed.log";
                    FileUtils.TryToDelete(itermediateFile);
                    FileUtils.TryToDelete(logFile);

                    /*var compressionRate = 100d * Math.Max(rules.GetDoubleValueOnly("RateLimit", 0.001), 
                            1d - rules.GetDoubleValueOnly("RateScale", 1d) * stage.TrianglesCount / inputTriangles);
                    Logging.Write($"Compression rate: {compressionRate:F2}% ({inputTriangles} on input, {stage.TrianglesCount} is the target)");
                    var args = new List<string>{ "-input-files", inputFile, "-ouput-samedir", "-ouput-namesuffix", $"_{filePostfix}" };
                    args.AddRange(new[]{ "-output-logfile", logFile });
                    args.AddRange(new[]{ "-level1", compressionRate.ToString(@"F2") });
                    args.AddRange(rules["Arguments"]?["Base"]?.ToObject<string[]>() ?? new string[0]);
                    if (useUV2) {
                        args.AddRange(rules["Arguments"]?["UV2"]?.ToObject<string[]>() ?? new string[0]);
                    }
                    if (stage.ApplyWeldingFix) {
                        args.AddRange(rules["Arguments"]?["Welding"]?.ToObject<string[]>() ?? new string[0]);
                    }*/

                    File.WriteAllText(scriptFile, $@"import pymeshlab
ms = pymeshlab.MeshSet()
ms.load_new_mesh('{Path.GetFileName(inputFile)}')
ms.meshing_decimation_quadric_edge_collapse_with_texture(targetfacenum={stage.TrianglesCount}, preserveboundary=False, boundaryweight=0.3, preservenormal=True)
ms.save_current_mesh('{Path.GetFileName(itermediateFile)}')");

                    string error = null;
                    await RunProcessAsync(_toolExecutable, new[] { Path.GetFileName(scriptFile) }, Path.GetDirectoryName(scriptFile),
                            true, progress, cancellationToken, err => error = err);

                    var outputFile = $@"{inputFile.ApartFromLast(@".dae")}_{filePostfix}.fbx";
                    await RunProcessAsync(Kn5.FbxConverterLocation, new[] { itermediateFile, outputFile, "/sffCOLLADA", "/dffFBX", "/f201300" },
                            Path.GetDirectoryName(scriptFile), true, null, cancellationToken, null).ConfigureAwait(false);

                    if (cacheKey != null) {
                        CacheStorage.Set(cacheKey, outputFile);
                    }
                    if (!File.Exists(outputFile)) {
                        var err = "MeshLab hasn’t created an output file";
                        /*try {
                            err = File.ReadAllLines(logFile).Skip(1).Where(x => x.Trim().Length > 0).JoinToString('\n').Or(err);
                        } catch {
                            // ignored
                        }*/
                        throw new Exception(error ?? err);
                    }
                    return outputFile;
                } finally {
                    if (!stage.KeepTemporaryFiles) {
                        if (!FileUtils.ArePathsEqual(rulesFilename, stage.ToolConfigurationFilename)) FileUtils.TryToDelete(rulesFilename);
                    }
                }
            }
        }

        public ICarLodGeneratorService Create(string toolExecutable, IReadOnlyList<ICarLodGeneratorStage> stages) {
            return new LodGeneratorService(toolExecutable, stages);
        }
    }
}