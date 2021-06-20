using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcTools;
using AcTools.AcdEncryption;
using AcTools.AcdFile;
using AcTools.ExtraKn5Utils.LodGenerator;
using AcTools.Kn5File;
using AcTools.Utils.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace AcManager.Tools.Tests {
    [TestFixture]
    public class CarLodGeneratorTest {
        private const string StageCockpitLr = @"CockpitLr";
        private const string StageLodB = @"LodB";
        private const string StageLodC = @"LodC";
        private const string StageLodD = @"LodD";

        private static JObject LoadCommonDefinitions() {
            try {
                return JObject.Parse(File.ReadAllText(@"C:\Users\Main\AppData\Local\AcTools Content Manager\Data\Car LODs Generation\CommonDefinitions.json"));
            } catch (Exception e) {
                AcToolsLogging.Write(e);
                return new JObject();
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        public sealed class StageParams : IWithId {
            public CarLodGeneratorStageParams Stage { get; }

            public string StageName { get; }

            public string SimplygonConfigurationFilename
                => $@"C:\Users\Main\AppData\Local\AcTools Content Manager\Data\Car LODs Generation\Stage.{StageName}.Rules.json";

            public StageParams(string displayName, string name, JObject definitions, Dictionary<string, string> userDefined) {
                StageName = name;
                Stage = new CarLodGeneratorStageParams(
                        name,
                        $@"C:\Users\Main\AppData\Local\AcTools Content Manager\Data\Car LODs Generation\Stage.{name}.json",
                        definitions, userDefined);
            }

            string IWithId<string>.Id => Stage.Id;
        }

        // [Test, Apartment(ApartmentState.STA)]
        public async Task Main() {
            Acd.Factory = new AcdFactory();
            Kn5.FbxConverterLocation = @"J:/LODgen/LODfolder/FbxConverter.exe";

            var definitions = LoadCommonDefinitions();
            var userDefined = new Dictionary<string, string> {
                ["CAR_PAINT"] = "material:body"
            };

            var stages = new List<StageParams> {
                //new StageParams("Low-res cockpit", StageCockpitLr, definitions, userDefined) { Stage = { DebugMode = !true } },
                new StageParams("LOD B", StageLodB, definitions, userDefined) { Stage = { } },
                //new StageParams("LOD C", StageLodC, definitions, userDefined) { Stage = { DebugMode = false } },
                //new StageParams("LOD D", StageLodD, definitions, userDefined),
            };

            var testSet = new[] {
                "ddm_honda_s2000_ap1-1"
                //"bmw_m3_e30"
                //"peugeot_504",
                /*"lada2101street",
                "ford_transit",
                "alfa_romeo_33",
                "bmw_m3_gt2",
                "chevy_nova_66_tuned"*/
            };

            var simplygon = "C:/Program Files/Simplygon/9/SimplygonBatch.exe";
            foreach (var carId in testSet) {
                var carLocation = @"D:\Games\Steam\SteamApps\common\assettocorsa\content\cars\" + carId;
                var generator = new CarLodGenerator(stages.Select(x => x.Stage), new CarLodSimplygonService(simplygon, stages), carLocation,
                        "J:/LODgen/temp_new");
                await generator.SaveInputModelAsync(stages[0].Stage, $"{carLocation}/debug.kn5");
                Process.Start($"{carLocation}/debug.kn5")?.WaitForExit();
            }
        }

        public class CarLodSimplygonService : ICarLodGeneratorService {
            private readonly string _simplygonExecutable;
            private readonly IReadOnlyList<StageParams> _stages;

            public CarLodSimplygonService(string simplygonExecutable, IReadOnlyList<StageParams> stages) {
                _simplygonExecutable = simplygonExecutable;
                _stages = stages;
            }

            private async Task RunProcessAsync(string filename, IEnumerable<string> args,
                    IProgress<double?> progress, CancellationToken cancellationToken) {
                var process = ProcessExtension.Start(filename, args, new ProcessStartInfo {
                    UseShellExecute = false,
                    RedirectStandardOutput = progress != null,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                });
                try {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (progress != null) {
                        process.OutputDataReceived += (sender, eventArgs) => {
                            if (!string.IsNullOrWhiteSpace(eventArgs.Data)) {
                                progress.Report(FlexibleParser.ParseDouble(eventArgs.Data, 0d) / 100d);
                            }
                        };
                    }
                    process.ErrorDataReceived += (sender, eventArgs) => {
                        if (!string.IsNullOrWhiteSpace(eventArgs.Data)) {
                            AcToolsLogging.Write(eventArgs.Data);
                        }
                    };
                    if (progress != null) {
                        process.BeginOutputReadLine();
                    }
                    process.BeginErrorReadLine();
                    await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                    if (process.ExitCode != 0) {
                        throw new Exception("Failed to run process: " + process.ExitCode);
                    }
                } finally {
                    if (!process.HasExitedSafe()) {
                        process.Kill();
                    }
                    process.Dispose();
                }
            }

            public async Task<string> GenerateLodAsync(string stageId, string inputFile, string modelChecksum,
                    IProgress<double?> progress, CancellationToken cancellationToken) {
                var intermediateFile = $@"{inputFile.ApartFromLast(".fbx")}_fixed.fbx";
                await RunProcessAsync(Kn5.FbxConverterLocation, new[] { inputFile, intermediateFile, "/sffFBX", "/dffFBX", "/f201300" },
                        null, cancellationToken).ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(0.05);

                /*await RunProcessAsync(_simplygonExecutable, new[] {
                    "-Progress",
                    _stages.GetById(stageId).SimplygonConfigurationFilename,
                    intermediateFile, outputFile
                }, progress.SubrangeDouble(0.05, 1.0), cancellationToken).ConfigureAwait(false);*/
                return null;
            }
        }
    }
}