using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.LodsTools;
using AcTools.ExtraKn5Utils.LodGenerator;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Helpers.LodGeneratorServices {
    public class PolygonCruncherToolDetails : ILodsToolDetails {
        public string Key => "PolygonCruncher";

        public bool UseFbx => true;

        public bool SplitPriorities => true;

        public IEnumerable<string> DefaultToolLocation
            => new[] { @"C:\Program Files\Polygon Cruncher 14\PolygonCruncher.exe", @"C:\Program Files\Polygon Cruncher 13\PolygonCruncher.exe" };

        public string FindTool(string currentLocation) {
            return FileRelatedDialogs.Open(new OpenDialogParams {
                DirectorySaveKey = "polygonCruncher",
                InitialDirectory = Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles")?.Replace(@" (x86)", "")
                        ?? @"C:\Program Files", @"Polygon Cruncher 13"),
                Filters = {
                    new DialogFilterPiece("Polygon Cruncher", "PolygonCruncher.exe"),
                    DialogFilterPiece.Applications,
                    DialogFilterPiece.AllFiles,
                },
                Title = "Select Polygon Cruncher",
                DefaultFileName = Path.GetFileName(currentLocation),
            });
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
                    string filePostfix = "crunched";
                    JToken rules;
                    try {
                        rules = JObject.Parse(File.ReadAllText(rulesFilename))[@"PolygonCruncher"];

                        var rulesData = rules.ToString();
                        var combinedChecksum = (modelChecksum + rulesData + inputTriangles + stage.ApplyWeldingFix + useUV2).GetChecksum();
                        filePostfix = combinedChecksum.Substring(0, 20);
                        cacheKey = $@"polygonCruncher:{combinedChecksum}";
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

                    var outputFile = $@"{inputFile.ApartFromLast(@".fbx")}_{filePostfix}1.fbx";
                    var logFile = $@"{inputFile.ApartFromLast(@".fbx")}_crunched.log";
                    FileUtils.TryToDelete(outputFile);
                    FileUtils.TryToDelete(logFile);

                    var compressionRate = 100d * Math.Max(rules.GetDoubleValueOnly("RateLimit", 0.001), 
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
                    }
                    using (var process = ProcessExtension.Start(_toolExecutable, args, new ProcessStartInfo {
                        UseShellExecute = false,
                        CreateNoWindow = true
                    })) {
                        try {
                            ChildProcessTracker.AddProcess(process);
                            cancellationToken.ThrowIfCancellationRequested();
                            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                        } finally {
                            if (!process.HasExitedSafe()) {
                                process.Kill();
                            }
                        }
                    }
                    
                    if (cacheKey != null) {
                        CacheStorage.Set(cacheKey, outputFile);
                    }
                    if (!File.Exists(outputFile)) {
                        var err = "Cruncher hasn’t created an output file";
                        try {
                            err = File.ReadAllLines(logFile).Skip(1).Where(x => x.Trim().Length > 0).JoinToString('\n').Or(err);
                            if (err.Contains("Batch processing is completed")) {
                                return string.Empty;
                            }
                        } catch {
                            // ignored
                        }
                        throw new Exception(err);
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