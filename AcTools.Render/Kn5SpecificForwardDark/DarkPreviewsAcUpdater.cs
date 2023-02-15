using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using Newtonsoft.Json;

namespace AcTools.Render.Kn5SpecificForwardDark {
    public class DarkPreviewsAcUpdater : IDarkPreviewsUpdater {
        public static bool OptionRunVisible = false;
        public static bool OptionKeepPositions = false;
        
        private readonly string _acRoot;
        private DarkPreviewsOptions _options;

        internal DarkPreviewsAcUpdater(string acRoot, DarkPreviewsOptions options) {
            _acRoot = acRoot;
            _options = options;
        }

        public void Dispose() { }

        private static async Task RunAssettoCorsaAsync(string acRoot, Func<bool> shutdownCheck, CancellationToken cancellation) {
            var process = Process.Start(new ProcessStartInfo {
                FileName = Path.Combine(acRoot, "acs.exe"),
                WorkingDirectory = acRoot
            }) ?? throw new Exception("Failed to start Assetto Corsa");
            await process.WaitKillAndDisposeAsync(shutdownCheck, cancellation).ConfigureAwait(false);
        }

        private static IDisposable Setup(string acRoot, DarkPreviewsOptions options, IEnumerable<Tuple<string, string, string>> items, string commentName) {
            var trackId = string.IsNullOrEmpty(options.Showroom) ? "../showroom/__missing0" : "../showroom/" + options.Showroom;
            var originalData = File.ReadAllBytes(AcPaths.GetRaceIniFilename());
            var cfg = new IniFile(AcPaths.GetRaceIniFilename());
            var exifComment = commentName ?? $"Settings checksum: {options.GetChecksum(true)}";
            var itemsList = items.ToList();
            cfg["__PREVIEW_GENERATION"].Set("ACTIVE", true);
            cfg["__PREVIEW_GENERATION"].Set("HIDDEN", !OptionRunVisible);
            cfg["__PREVIEW_GENERATION"].Set("PRESET", JsonConvert.SerializeObject(options).ToCutBase64());
            cfg["__PREVIEW_GENERATION"].Set("COMMENT", exifComment.ToCutBase64());
            cfg["BENCHMARK"].Set("ACTIVE", false);
            cfg["REMOTE"].Set("ACTIVE", false);
            cfg["REPLAY"].Set("ACTIVE", false);
            cfg["RESTART"].Set("ACTIVE", false);
            cfg["RACE"].Set("MODEL", itemsList[0].Item1);
            cfg["RACE"].Set("SKIN", itemsList[0].Item1);
            cfg["CAR_0"].Set("SKIN", itemsList[0].Item2);
            cfg["RACE"].Set("TRACK", trackId);
            cfg["RACE"].Set("CONFIG_TRACK", "");
            cfg["RACE"].Set("CARS", itemsList.Count);
            for (var i = 0; i < itemsList.Count; i++) {
                var item = itemsList[i];
                cfg["__PREVIEW_GENERATION"].Set("DESTINATION_" + i, item.Item3);
                cfg["CAR_" + i].Set("MODEL", i == 0 ? "-" : item.Item1);
                cfg["CAR_" + i].Set("SKIN", item.Item2);
            }
            cfg.Save();
            File.WriteAllText(Path.Combine(acRoot, "steam_appid.txt"), "244210");
            return new ActionAsDisposable(() => File.WriteAllBytes(AcPaths.GetRaceIniFilename(), originalData));
        }

        public void Shot(string carId, string skinId, string destination = null, DataWrapper carData = null,
                ImageUtils.ImageInformation information = null, Action callback = null) {
            CleanPreviewState(_acRoot, carId);
            using (new ActionAsDisposable(() => CleanPreviewState(_acRoot, carId)))
            using (Setup(_acRoot, _options, new List<Tuple<string, string, string>> { Tuple.Create(carId, skinId, destination) }, information?.Comment)) {
                var process = Process.Start(new ProcessStartInfo {
                    FileName = Path.Combine(_acRoot, "acs.exe"),
                    WorkingDirectory = _acRoot
                }) ?? throw new Exception("Failed to start Assetto Corsa");
                process.WaitForExit();
                callback?.Invoke();
            }
        }

        public async Task ShotAsync(string carId, string skinId, string destination = null, DataWrapper carData = null,
                ImageUtils.ImageInformation information = null, Action callback = null,
                Func<bool> shutdownCheck = null, CancellationToken cancellation = default(CancellationToken)) {
            CleanPreviewState(_acRoot, carId);
            using (new ActionAsDisposable(() => CleanPreviewState(_acRoot, carId)))
            using (Setup(_acRoot, _options, new List<Tuple<string, string, string>> { Tuple.Create(carId, skinId, destination) }, information?.Comment)) {
                await RunAssettoCorsaAsync(_acRoot, shutdownCheck, cancellation).ConfigureAwait(false);
                callback?.Invoke();
            }
        }

        public static async Task ShotSeriesAsync(string acRoot, DarkPreviewsOptions options, IEnumerable<Tuple<string, string, string>> items,
                string comment, Func<bool> shutdownCheck, CancellationToken cancellation) {
            using (Setup(acRoot, options, items, comment)) {
                if (cancellation.IsCancellationRequested) return;
                await RunAssettoCorsaAsync(acRoot, shutdownCheck, cancellation).ConfigureAwait(false);
            }
        }

        public void SetOptions(DarkPreviewsOptions options) {
            _options = options;
        }

        public async Task WaitForProcessing() {
            await Task.Delay(0);
        }

        public static void CleanPreviewState(string acRoot, string carId) {
            if (OptionKeepPositions) return;
            FileUtils.TryToDelete(Path.Combine(acRoot, "cache", "preview_state", carId + ".bin"));
        }
    }
}