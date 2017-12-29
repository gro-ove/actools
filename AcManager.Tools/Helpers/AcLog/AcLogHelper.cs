using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers.AcSettings;
using AcManager.Tools.Managers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.AcLog {
    public static class AcLogHelper {
        private static readonly List<IAcLogHelperExtension> Extensions = new List<IAcLogHelperExtension>();

        public static void AddExtension([NotNull] IAcLogHelperExtension extension) {
            Extensions.Add(extension);
        }

        private static string TryToGetCarName(string log) {
            var m = Regex.Matches(log, @"\bcontent/cars/([\w-]+)").Cast<Match>().LastOrDefault();
            var id = m?.Success != true ? null : m.Groups[1].Value;
            return string.IsNullOrWhiteSpace(id) ? @"?" : CarsManager.Instance.GetById(id)?.DisplayName ?? $"“{id}”";
        }

        [CanBeNull]
        public static WhatsGoingOn TryToDetermineWhatsGoingOn() {
            try {
                var log = File.Open(AcPaths.GetLogFilename(), FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite).ReadAsStringAndDispose();

                {
                    var match = Regex.Match(log,
                            @"ERROR: INIReader: content/cars/([\w._-]+)/data/car\.ini > KEY_NOT_FOUND: \[INFO\] SCREEN_NAME",
                            RegexOptions.CultureInvariant);
                    if (match.Success) {
                        return new WhatsGoingOn(
                                CarsManager.Instance.GetWrapperById(match.Groups[1].Value) == null
                                        ? WhatsGoingOnType.CarIsMissing : WhatsGoingOnType.CarIsMissingOrDamaged,
                                match.Groups[1].Value);
                    }
                }

                if (log.Contains(@"ACClient:: ACP_WRONG_PASSWORD")) {
                    return new WhatsGoingOn(WhatsGoingOnType.OnlineWrongPassword);
                }

                if (log.Contains(@"ERROR: RaceManager :: Handshake FAILED")) {
                    return new WhatsGoingOn(WhatsGoingOnType.OnlineConnectionFailed);
                }

                if (log.Contains(@"ERROR: Cannot create suspension type")) {
                    return new WhatsGoingOn(WhatsGoingOnType.GameMightBeObsolete);
                }

                if (log.Contains(@"COULD NOT FIND SUSPENSION OBJECT WHEEL_")) {
                    return new WhatsGoingOn(WhatsGoingOnType.WheelsAreMissing);
                }

                if (log.Contains(@"): AnimationBlender::blendAnimations") && log.Contains(@"): DriverModel::animateHShifter")) {
                    return new WhatsGoingOn(WhatsGoingOnType.SteerAnimIsMissing);
                }

                {
                    var match = Regex.Match(log, @"Error, cannot initialize Post Processing, (.+) not found", RegexOptions.CultureInvariant);
                    if (match.Success) {
                        return new WhatsGoingOn(WhatsGoingOnType.PpFilterIsMissing, match.Groups[1].Value);
                    }
                }

                var i = log.IndexOf(@"CRASH in:", StringComparison.Ordinal);
                string crash;
                if (i != -1) {
                    crash = log.Substring(i);
                    if (crash.Contains(@" evaluateTimeFromTrackSpline")) {
                        return new WhatsGoingOn(WhatsGoingOnType.TimeAttackNotSupported);
                    }

                    if (crash.Contains(@"PyTraceBack_Print")) {
                        var inits = Regex.Matches(log, @"\bInit PyPlugin:([^\n\r]+)").OfType<Match>().LastOrDefault();
                        if (inits?.Success == true) {
                            var appId = inits.Groups[1].Value.Trim();
                            return new WhatsGoingOn(WhatsGoingOnType.AppMightBeBroken, appId){
                                Fix = AcSettingsHolder.Python.IsActivated(appId) ? token => {
                                    AcSettingsHolder.Python.SetActivated(appId, false);
                                    return Task.Delay(0);
                                } : (Func<CancellationToken, Task>)null,
                                FixDisplayName = "Disable app"
                            };
                        }
                    }

                    if (crash.Contains(@"SkyBox::updateCloudsGeneration")) {
                        return new WhatsGoingOn(WhatsGoingOnType.CloudsMightBeMissing);
                    }

                    if (crash.Contains(@"DriverModel::DriverModel")) {
                        return new WhatsGoingOn(WhatsGoingOnType.DriverModelIsMissing);
                    }

                    if (crash.Contains(@"KGLTexture::initSize")) {
                        return new WhatsGoingOn(WhatsGoingOnType.WrongTextureFormat);
                    }

                    if (crash.Contains(@"AISpline::getPointWithOffset") || crash.Contains(@"AISpline::calculateNormals")) {
                        return new WhatsGoingOn(WhatsGoingOnType.AiSplineMissing);
                    }

                    if (crash.Contains(@"\drivetrain.cpp")) {
                        return new WhatsGoingOn(WhatsGoingOnType.DrivetrainIsDamaged, TryToGetCarName(log.Substring(0, i)));
                    }

                    if (crash.Contains(@"\analoginstruments.cpp")) {
                        return new WhatsGoingOn(WhatsGoingOnType.AnalogInstrumentsAreDamaged, TryToGetCarName(log.Substring(0, i)));
                    }
                } else {
                    crash = null;
                }

                var extension = Extensions.Select(x => x.Detect(log, crash)).NonNull().FirstOrDefault();
                if (extension != null) {
                    return extension;
                }
            } catch (Exception e) {
                Logging.Write("Can’t determine what’s going on: " + e);
            }

            return null;
        }
    }
}