using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.ContentInstallation;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Managers;
using AcTools.Kn5File;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using ZipArchive = SharpCompress.Archives.Zip.ZipArchive;

namespace AcManager.Tools.Miscellaneous {
    public static class Fixes {
        public static Task FixMissingKsSkyBoxShader(string filename, CancellationToken cancellation) {
            return Task.Run(() => {
                var backup = FileUtils.EnsureUnique(filename + @".bak");
                File.Move(filename, backup);
                cancellation.ThrowIfCancellationRequested();

                var kn5 = Kn5.FromFile(backup);
                cancellation.ThrowIfCancellationRequested();

                var changed = false;
                foreach (var material in kn5.Materials.Values) {
                    if (material.ShaderName == @"ksSkyBox") {
                        material.ShaderName = @"ksSky";
                        material.TextureMappings = new Kn5Material.TextureMapping[0];
                        changed = true;
                    }
                }

                if (!changed) {
                    throw new InformativeException("Nothing to fix", "None of materials is using ksSkyBox shader");
                }

                cancellation.ThrowIfCancellationRequested();
                kn5.Save(filename);
            }, cancellation); 
        }

        public static Task FixMissingDefaultPpFilter(CancellationToken cancellation) {
            return Task.Run(() => {
                var original = CmApiProvider.GetData("static/get/pp_default");
                cancellation.ThrowIfCancellationRequested();
                if (original == null) throw new InformativeException("Can’t load original filter");

                using (var stream = new MemoryStream(original))
                using (var zip = ZipArchive.Open(stream)) {
                    var entry = zip.Entries.FirstOrDefault(x => x.Key == @"default.ini");
                    if (entry == null) throw new Exception("Invalid data");

                    File.WriteAllBytes(PpFiltersManager.Instance.DefaultFilename, entry.OpenEntryStream().ReadAsBytesAndDispose());
                }
            }, cancellation); 
        }

        private static byte[] Decompress(byte[] data) {
            return new DeflateStream(new MemoryStream(data), CompressionMode.Decompress).ReadAsBytesAndDispose();
        }

        public static Func<CancellationToken, Task> FixOldFlames(IEnumerable<string> carIds) {
            return cancellation => Task.Run(() => {
                var flamesTextures = CmApiProvider.GetData("static/get/flames");

                foreach (var car in carIds.Select(x => CarsManager.Instance.GetById(x)).Where(x => x != null)) {
                    if (cancellation.IsCancellationRequested) return;

                    var data = car.AcdData;
                    if (data == null || data.IsEmpty) continue;

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

                    var flamesDirectory = Path.Combine(car.Location, @"texture", @"flames");
                    flamesTextures.ExtractAsArchiveTo(flamesDirectory);
                }
            }, cancellation);
        }

        public static Func<CancellationToken, Task> FixMissingFlamesTextures(IEnumerable<string> carIds) {
            return cancellation => Task.Run(() => {
                var flamesTextures = CmApiProvider.GetData("static/get/flames");

                foreach (var car in carIds.Select(x => CarsManager.Instance.GetById(x)).Where(x => x != null)) {
                    if (cancellation.IsCancellationRequested) return;

                    var flamesDirectory = Path.Combine(car.Location, @"texture", @"flames");
                    flamesTextures.ExtractAsArchiveTo(flamesDirectory);
                }
            }, cancellation);
        }
    }

    public static class AcLogHelper {
        public class WhatsGoingOn {
            public WhatsGoingOnType Type { get; }

            public object[] Arguments { get; }

            /// <summary>
            /// Throws an exception if fixing failed.
            /// </summary>
            public Func<CancellationToken, Task> Fix { get; internal set; }

            internal WhatsGoingOn(WhatsGoingOnType type, params object[] arguments) {
                Type = type;
                Arguments = arguments;
            }

            public string GetDescription() {
                return string.Format(Type.GetDescription() ?? Type.ToString(), Arguments);
            }

            private NonfatalErrorSolution _solution;

            public NonfatalErrorSolution Solution => _solution ?? (Fix == null ? null :
                    _solution = new NonfatalErrorSolution(null, null, Fix, () => Fix != null));
        }

        public enum WhatsGoingOnType {
            [LocalizedDescription("LogHelper_PasswordIsInvalid")]
            OnlineWrongPassword,

            [LocalizedDescription("LogHelper_CannotConnectToRemoteServer")]
            OnlineConnectionFailed,

            [LocalizedDescription("LogHelper_SuspensionIsMissing")]
            SuspensionIsMissing,

            [LocalizedDescription("LogHelper_WheelsAreMissing")]
            WheelsAreMissing,

            [LocalizedDescription("LogHelper_DriverIsMissing")]
            DriverModelIsMissing,

            [LocalizedDescription("LogHelper_TimeAttackNotSupported")]
            TimeAttackNotSupported,

            [LocalizedDescription("LogHelper_DefaultPpFilterIsMissing")]
            DefaultPpFilterIsMissing,

            [LocalizedDescription("LogHelper_PpFilterIsMissing")]
            PpFilterIsMissing,

            [LocalizedDescription("LogHelper_ShaderIsMissing")]
            ShaderIsMissing,

            [LocalizedDescription("LogHelper_CloudsMightBeMissing")]
            CloudsMightBeMissing,

            // TRANSLATE ME
            [Description("Game might be obsolete")]
            GameMightBeObsolete,

            [Description("Model is obsolete; please, consider updating flames to the second version")]
            FlamesV1TextureNotFound,

            [Description("Flames textures are missing")]
            FlamesFlashTexturesAreMissing
        }

        private static IEnumerable<string> GetCarsIds(string log) {
            return Regex.Matches(log, @"content/cars/(\w+)").Cast<Match>().Select(x => x.Groups[1].Value).Distinct().ToList();
        }

        [CanBeNull]
        public static WhatsGoingOn TryToDetermineWhatsGoingOn() {
            try {
                var log = File.Open(FileUtils.GetLogFilename(), FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite).ReadAsStringAndDispose();

                if (log.Contains(@"ACClient:: ACP_WRONG_PASSWORD")) {
                    return new WhatsGoingOn(WhatsGoingOnType.OnlineWrongPassword);
                }

                if (log.Contains(@"ERROR: RaceManager :: Handshake FAILED")) {
                    return new WhatsGoingOn(WhatsGoingOnType.OnlineConnectionFailed);
                }

                if (log.Contains(@"ERROR: Cannot create suspension type")) {
                    return new WhatsGoingOn(WhatsGoingOnType.GameMightBeObsolete);
                }

                if (log.Contains(@"Flame V1: texture not found")) {
                    return new WhatsGoingOn(WhatsGoingOnType.FlamesV1TextureNotFound) {
                        Fix = Fixes.FixOldFlames(GetCarsIds(log))
                    };
                }

                if (log.Contains(@"FlameStart : Flash textures are missing!")) {
                    return new WhatsGoingOn(WhatsGoingOnType.FlamesFlashTexturesAreMissing) {
                        Fix = Fixes.FixMissingFlamesTextures(GetCarsIds(log))
                    };
                }
                
                if (log.Contains(@"COULD NOT FIND SUSPENSION OBJECT SUSP_")) {
                    return new WhatsGoingOn(WhatsGoingOnType.SuspensionIsMissing);
                }
                
                if (log.Contains(@"COULD NOT FIND SUSPENSION OBJECT WHEEL_")) {
                    return new WhatsGoingOn(WhatsGoingOnType.WheelsAreMissing);
                }
                
                if (log.Contains(@"Error, cannot initialize Post Processing, system/cfg/ppfilters/default.ini not found")) {
                    return new WhatsGoingOn(WhatsGoingOnType.DefaultPpFilterIsMissing) {
                        Fix = Fixes.FixMissingDefaultPpFilter
                    };
                }

                {
                    var match = Regex.Match(log, @"ERROR: Shader (.+) NOT FOUND, RETURNING NULL");
                    if (match.Success) {
                        var shader = match.Groups[1].Value;
                        Func<CancellationToken, Task> fix = null;

                        if (shader == @"ksSkyBox" && AcRootDirectory.Instance.Value != null) {
                            var model = Regex.Matches(log, @"LOADING MODEL (.+\.kn5)").OfType<Match>().LastOrDefault();
                            if (model?.Success == true) {
                                var filename = Path.Combine(AcRootDirectory.Instance.Value, model.Groups[1].Value);
                                fix = c => Fixes.FixMissingKsSkyBoxShader(filename, c);
                            }
                        }

                        return new WhatsGoingOn(WhatsGoingOnType.ShaderIsMissing, match.Groups[1].Value) {
                            Fix = fix
                        };
                    }
                }

                {
                    var match = Regex.Match(log, @"Error, cannot initialize Post Processing, (.+) not found", RegexOptions.CultureInvariant);
                    if (match.Success) return new WhatsGoingOn(WhatsGoingOnType.PpFilterIsMissing, match.Groups[1].Value);
                }

                var i = log.IndexOf(@"CRASH in:", StringComparison.Ordinal);
                if (i == -1) return null;

                var crash = log.Substring(i);
                if (crash.Contains(@" evaluateTimeFromTrackSpline")) {
                    return new WhatsGoingOn(WhatsGoingOnType.TimeAttackNotSupported);
                }

                if (crash.Contains(@"SkyBox::updateCloudsGeneration")) {
                    return new WhatsGoingOn(WhatsGoingOnType.CloudsMightBeMissing);
                }
                
                if (crash.Contains(@"DriverModel::DriverModel")) {
                    return new WhatsGoingOn(WhatsGoingOnType.DriverModelIsMissing);
                }
            } catch (Exception e) {
                Logging.Write("Can’t determine what’s going on: " + e);
            }

            return null;
        }
    }
}