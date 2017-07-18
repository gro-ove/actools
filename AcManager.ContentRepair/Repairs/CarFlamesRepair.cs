using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.ContentRepair.Repairs {
    public class CarFlamesRepair : CarRepairBase {
        private static byte[] _flamesTextures;

        private static async Task<byte[]> GetFlamesTexturesAsync(IProgress<AsyncProgressEntry> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            if (_flamesTextures == null) {
                progress?.Report(AsyncProgressEntry.FromStringIndetermitate("Loading flames textures…"));
                _flamesTextures = await CmApiProvider.GetStaticDataBytesAsync("flames", cancellation: cancellation);
                if (cancellation.IsCancellationRequested) return null;
            }

            return _flamesTextures;
        }

        public override bool AffectsData => true;

        public static async Task<bool> UpgradeToSecondVersionAsync(CarObject car, IProgress<AsyncProgressEntry> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            progress?.Report(AsyncProgressEntry.FromStringIndetermitate("Updating data…"));
            if (!await Task.Run(() => {
                var data = car.AcdData;
                if (data == null || data.IsEmpty) return false;

                var flames = data.GetIniFile("flames.ini");
                var header = flames["HEADER"];
                header.Set("EDIT_BIG", false);
                header.Set("EDIT_STATE", 4);
                header.Set("BURN_FUEL_MULT", 10);
                header.Set("FLASH_THRESHOLD", 7);

                foreach (var section in flames.GetSections("FLAME")) {
                    section.Set("IS_LEFT", section.GetVector3("POSITION").FirstOrDefault() < 0d);
                    section.Set("GROUP", 0);
                }

                flames.Save();
                Logging.Write($"Fixed: flames.ini of {car.DisplayName}");

                var flamesPresetsEncoded = @"jdPBjoIwEAbgOwlvUjedgSJ74MDGgia4kFIPaja8/1s4BVdaW1IPcIDhy/Dzcz/K+iDVX5qMp5uczpdOV5AmaXIflBy" +
                        @"lnkZdKz1xGtDq1LZSVTxN+qahexX/4ux54AKYS4JGr4M0c6r9qVAIBiVnIGgOfRosGgI0vGR4wmDByBnOk3tfRkvG0NLluvQ+sPTLLm1b/h6cO" +
                        @"PJIHEVg628aWl7NdSG2cbG6+bbrhmFgjHw/8F0MuMJ2u74ftosBbEfn2V5jhsxdGrBkIpDFTG8Wg5pkbDR9Kj6xTWxv+GY3stnO6alMrLZwQ7Ft" +
                        @"J5Omq8ejE0rwM3I/bqt4/2mDL8d+Fp59JLuBLHSsInb3Ap0mGpatHw==";
                var flamesPresets = data.GetRawFile(@"flame_presets.ini");
                flamesPresets.Content = Encoding.UTF8.GetString(new DeflateStream(
                        new MemoryStream(Convert.FromBase64String(flamesPresetsEncoded)),
                        CompressionMode.Decompress).ReadAsBytesAndDispose());
                flamesPresets.Save();
                Logging.Write($"Fixed: flame_presets.ini of {car.DisplayName}");

                return true;
            }) || cancellation.IsCancellationRequested) return false;
            return await FixMissingTexturesAsync(car, progress, cancellation);
        }

        public static async Task<bool> FixMissingTexturesAsync(CarObject car, IProgress<AsyncProgressEntry> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            var flamesTextures = await GetFlamesTexturesAsync(progress, cancellation);
            if (cancellation.IsCancellationRequested) return false;

            progress?.Report(AsyncProgressEntry.FromStringIndetermitate("Unpacking textures…"));
            return await Task.Run(() => {
                var flamesDirectory = Path.Combine(car.Location, @"texture", @"flames");
                flamesTextures.ExtractAsArchiveTo(flamesDirectory);
                return true;
            });
        }

        private static ContentRepairSuggestion TestFlamesVersion(CarObject car, out bool hasFlames) {
            var data = car.AcdData;
            if (data == null) {
                hasFlames = false;
                return null;
            }

            hasFlames = data.GetIniFile("flames.ini").GetSections("FLAME").Any();
            if (!hasFlames || data.GetRawFile(@"flame_presets.ini").Content.Length != 0) return null;

            return new ContentObsoleteSuggestion("Obsolete flames", "First version of flames used, but the second one is already available.",
                (p, c) => UpgradeToSecondVersionAsync(car, p, c)) {
                AffectsData = true
            };
        }

        private static ContentRepairSuggestion TestFlamesTexturesExistance(CarObject car, bool firstVersion) {
            var dir = new DirectoryInfo(Path.Combine(car.Location, "texture", "flames"));
            var regex = firstVersion ? new Regex(@"^\d+\.png$", RegexOptions.IgnoreCase) :
                    new Regex(@"^[flx]\d+\.dds$", RegexOptions.IgnoreCase);
            if (dir.Exists && dir.GetFiles().Any(x => regex.IsMatch(x.Name))) return null;
            return new ContentObsoleteSuggestion("Flames textures missing", "It was all right before, but now it will cause game to crash.",
                    (p, c) => FixMissingTexturesAsync(car, p, c));
        }

        public override IEnumerable<ContentRepairSuggestion> GetSuggestions(CarObject car) {
            bool hasFlames;
            var obsoleteFlames = TestFlamesVersion(car, out hasFlames);
            var missingFlames = hasFlames ? TestFlamesTexturesExistance(car, obsoleteFlames != null) : null;
            return new[] {
                obsoleteFlames, missingFlames
            }.NonNull();
        }
    }
}
