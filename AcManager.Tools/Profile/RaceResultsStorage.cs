using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.SemiGui;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Profile {
    public class QuickDrivePresetProperty {
        public QuickDrivePresetProperty(string serializedData) {
            SerializedData = serializedData;
        }

        public string SerializedData { get; }
    }

    public class RaceResultsStorage {
        public static string KeyRaceIni = "__raceIni";
        public static string KeyQuickDrive = "__quickDrive";

        public string SessionsDirectory { get; private set; }

        private static RaceResultsStorage _instance;

        public static RaceResultsStorage Instance => _instance ?? (_instance = new RaceResultsStorage());

        public void SetListener() {
            SessionsDirectory = FilesStorage.Instance.GetDirectory("Progress", "Sessions");
            GameWrapper.Finished += OnGameFinished;
        }

        private void OnGameFinished(object sender, GameFinishedArgs gameFinishedArgs) {
            var result = gameFinishedArgs.Result;
            if (result == null) return;
            Task.Run(() => {
                AddNewResult(gameFinishedArgs.StartProperties, result);

                var toRemoval = new DirectoryInfo(SessionsDirectory)
                        .GetFiles("*.json").OrderByDescending(x => x.LastWriteTime).Skip(SettingsHolder.Drive.RaceResultsLimit).ToList();
                Logging.Debug($"Removing {toRemoval.Count} old race result(s): {toRemoval.Select(x => x.Name).JoinToString(", ")}");
                FileUtils.Recycle(toRemoval.Select(x => x.FullName).ToArray());
            });
        }

        public void AddNewResult([NotNull] Game.StartProperties startProperties, [NotNull] Game.Result result) {
            var directory = SessionsDirectory;
            FileUtils.EnsureDirectoryExists(directory);

            var fileName = DateTime.Now.ToString("yyMMdd-HHmmss") + ".json";
            var raceOut = AcPaths.GetResultJsonFilename();

            JObject data = null;
            if (File.Exists(raceOut)) {
                try {
                    // Let’s try to save original data instead in case we missed something during parsing.
                    // But let’s remove formatting just in case.
                    data = JObject.Parse(File.ReadAllText(raceOut));
                } catch (Exception e) {
                    Logging.Warning(e);
                }
            }

            if (data == null) {
                data = JObject.FromObject(result);
            }

            // Trying to keep race params as well…
            // TODO: Do it the other way around, from start params?

            var raceIni = AcPaths.GetRaceIniFilename();
            if (File.Exists(raceIni)) {
                data[KeyRaceIni] = File.ReadAllText(raceIni);
            }

            var quickDrive = startProperties.GetAdditional<QuickDrivePresetProperty>();
            if (quickDrive?.SerializedData != null) {
                data[KeyQuickDrive] = quickDrive.SerializedData;
            }

            File.WriteAllText(FileUtils.EnsureUnique(Path.Combine(directory, fileName)), data.ToString(Formatting.None));
        }
    }
}