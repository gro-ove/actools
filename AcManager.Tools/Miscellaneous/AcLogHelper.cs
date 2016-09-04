using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Managers;
using AcTools.Kn5File;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using SharpCompress.Archive.Zip;

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
    }

    public static class AcLogHelper {
        public class WhatsGoingOn : INonfatalErrorSolution {
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
                return string.Format(Type.GetDescription(), Arguments);
            }

            string INonfatalErrorSolution.DisplayName => null;

            bool INonfatalErrorSolution.CanBeApplied => Fix != null;

            Task INonfatalErrorSolution.Apply(CancellationToken cancellationToken) {
                return Fix(cancellationToken);
            }
        }

        public enum WhatsGoingOnType {
            [LocalizedDescription("PasswordIsInvalid")]
            OnlineWrongPassword,

            [LocalizedDescription("CannotConnectToRemoteServer")]
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