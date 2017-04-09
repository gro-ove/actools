using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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

                if (log.Contains(@"COULD NOT FIND SUSPENSION OBJECT WHEEL_")) {
                    return new WhatsGoingOn(WhatsGoingOnType.WheelsAreMissing);
                }

                {
                    var match = Regex.Match(log, @"Error, cannot initialize Post Processing, (.+) not found", RegexOptions.CultureInvariant);
                    if (match.Success) return new WhatsGoingOn(WhatsGoingOnType.PpFilterIsMissing, match.Groups[1].Value);
                }

                var i = log.IndexOf(@"CRASH in:", StringComparison.Ordinal);
                string crash;
                if (i != -1) {
                    crash = log.Substring(i);
                    if (crash.Contains(@" evaluateTimeFromTrackSpline")) {
                        return new WhatsGoingOn(WhatsGoingOnType.TimeAttackNotSupported);
                    }

                    if (crash.Contains(@"SkyBox::updateCloudsGeneration")) {
                        return new WhatsGoingOn(WhatsGoingOnType.CloudsMightBeMissing);
                    }

                    if (crash.Contains(@"DriverModel::DriverModel")) {
                        return new WhatsGoingOn(WhatsGoingOnType.DriverModelIsMissing);
                    }

                    if (crash.Contains(@"AISpline::getPointWithOffset") || crash.Contains(@"AISpline::calculateNormals")) {
                        return new WhatsGoingOn(WhatsGoingOnType.AiSplineMissing);
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