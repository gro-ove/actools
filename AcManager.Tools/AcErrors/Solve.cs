using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.AcErrors.Solutions;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.AcErrors {
    public static class Solve {
        [NotNull]
        public static IEnumerable<Solution> TryToFindRenamedFile(string baseDirectory, string filename, bool skipOff = false) {
            return FileUtils.FindRenamedFile(baseDirectory, filename)
                            .Where(x => skipOff == false || x.EndsWith(@"-off", StringComparison.OrdinalIgnoreCase))
                            .Select(x => new Solution(string.Format(ToolsStrings.Solving_RestoreFrom, x.Substring(baseDirectory.Length)),
                                    ToolsStrings.Solving_RestoreFrom_Commentary,
                                    e => {
                                        var directory = Path.GetDirectoryName(filename);
                                        if (directory == null) throw new IOException("directory = null");

                                        if (!Directory.Exists(directory)) {
                                            Directory.CreateDirectory(directory);
                                        }

                                        if (File.Exists(filename)) {
                                            FileUtils.Recycle(filename);
                                        }

                                        File.Move(x, filename);
                                    }));
        }

        [ItemCanBeNull]
        public static async Task<ISolution> TryToFindMissingCarAsync(string carId) {
            var found = await IndexDirectDownloader.IsCarAvailableAsync(carId);
            if (found == IndexDirectDownloader.ContentState.Available) {
                return new AsyncSolution("Install missing car", 
                        "The missing car is found, do you want to try to download it?",
                        (error, token) => IndexDirectDownloader.DownloadCarAsync(carId));
            }
            if (found == IndexDirectDownloader.ContentState.Limited) {
                return new AsyncSolution("Try to download missing car", 
                        "Link to the missing car found, would you like to open it?",
                        (error, token) => IndexDirectDownloader.DownloadCarAsync(carId));
            }

            Logging.Warning($"Car “{carId}” has not been found!");
            return null;
        }

        [ItemCanBeNull]
        public static async Task<ISolution> TryToFindMissingTrackAsync(string trackId) {
            var found = await IndexDirectDownloader.IsTrackAvailableAsync(trackId);
            if (found == IndexDirectDownloader.ContentState.Available) {
                return new AsyncSolution("Install missing track", 
                        "The missing track is found, do you want to try to download it?",
                        (error, token) => IndexDirectDownloader.DownloadTrackAsync(trackId));
            }
            if (found == IndexDirectDownloader.ContentState.Limited) {
                return new AsyncSolution("Try to download missing track", 
                        "Link to the missing track found, would you like to open it?",
                        (error, token) => IndexDirectDownloader.DownloadTrackAsync(trackId));
            }

            Logging.Warning($"Track “{trackId}” not found!");
            return null;
        }

        [NotNull]
        public static IEnumerable<Solution> TryToFindAnyFile(string baseDirectory, string filename, string searchPattern) {
            return Directory.GetFiles(baseDirectory, searchPattern)
                            .Select(x => new Solution(string.Format(ToolsStrings.Solving_RestoreFrom, x.Substring(baseDirectory.Length)),
                                    ToolsStrings.Solving_RestoreFrom_Commentary,
                                    e => {
                                        var directory = Path.GetDirectoryName(filename);
                                        if (directory == null) throw new IOException("directory = null");

                                        if (!Directory.Exists(directory)) {
                                            Directory.CreateDirectory(directory);
                                        }

                                        if (File.Exists(filename)) {
                                            FileUtils.Recycle(filename);
                                        }

                                        File.Move(x, filename);
                                    }));
        }

        public static bool TryToRestoreDamagedJsonFile(string filename, JObjectRestorationScheme scheme) {
            var data = File.ReadAllText(filename);
            var jObject = JsonExtension.TryToRestore(data, scheme);
            if (jObject == null) return false;

            FileUtils.Recycle(filename);
            File.WriteAllText(filename, jObject.ToString());
            return true;
        }

        [CanBeNull]
        public static MultiSolution TryToCreateNewFile(AcJsonObjectNew target) {
            if (target is ShowroomObject) {
                return new MultiSolution(
                    ToolsStrings.Solving_CreateNewFile,
                    ToolsStrings.Solving_CreateNewFile_Commentary,
                    e => {
                        var jObject = new JObject {
                            [@"name"] = AcStringValues.NameFromId(e.Target.Id)
                        };

                        FileUtils.EnsureFileDirectoryExists(((AcJsonObjectNew)e.Target).JsonFilename);
                        File.WriteAllText(((AcJsonObjectNew)e.Target).JsonFilename, jObject.ToString());
                    });
            }

            if (target is CarSkinObject) {
                return new MultiSolution(
                    ToolsStrings.Solving_CreateNewFile,
                    ToolsStrings.Solving_CreateNewFile_Commentary,
                    e => {
                        var jObject = new JObject {
                            [@"skinname"] = AcStringValues.NameFromId(e.Target.Id),
                            [@"drivername"] = "",
                            [@"country"] = "",
                            [@"team"] = "",
                            [@"number"] = @"0"
                        };

                        if (!SettingsHolder.Content.SkinsSkipPriority) {
                            jObject[@"priority"] = 1;
                        }

                        FileUtils.EnsureFileDirectoryExists(((AcJsonObjectNew)e.Target).JsonFilename);
                        File.WriteAllText(((AcJsonObjectNew)e.Target).JsonFilename, jObject.ToString());
                    });
            }

            return null;
        }
    }
}
