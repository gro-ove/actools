using System;
using System.IO;
using System.Text.RegularExpressions;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Miscellaneous {
    public static class AcLogHelper {
        public enum WhatsGoingOn {
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

            [LocalizedDescription("LogHelper_TimeAttackNotSupported")]
            PpFilterIsMissing,
        }

        public static WhatsGoingOn? TryToDetermineWhatsGoingOn() {
            try {
                var log = File.Open(FileUtils.GetLogFilename(), FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite).ReadAsStringAndDispose();

                if (log.Contains(@"ACClient:: ACP_WRONG_PASSWORD")) {
                    return WhatsGoingOn.OnlineWrongPassword;
                }

                if (log.Contains(@"ERROR: RaceManager :: Handshake FAILED")) {
                    return WhatsGoingOn.OnlineConnectionFailed;
                }
                
                if (log.Contains(@"COULD NOT FIND SUSPENSION OBJECT SUSP_")) {
                    return WhatsGoingOn.SuspensionIsMissing;
                }
                
                if (log.Contains(@"COULD NOT FIND SUSPENSION OBJECT WHEEL_")) {
                    return WhatsGoingOn.WheelsAreMissing;
                }
                
                if (log.Contains(@"Error, cannot initialize Post Processing, system/cfg/ppfilters/default.ini not found")) {
                    return WhatsGoingOn.DefaultPpFilterIsMissing;
                }
                
                if (Regex.IsMatch(log, @"Error, cannot initialize Post Processing, .+ not found", RegexOptions.CultureInvariant)) {
                    return WhatsGoingOn.PpFilterIsMissing;
                }

                var i = log.IndexOf(@"CRASH in:", StringComparison.Ordinal);
                if (i == -1) return null;

                var crash = log.Substring(i);
                if (crash.Contains(@" evaluateTimeFromTrackSpline")) {
                    return WhatsGoingOn.TimeAttackNotSupported;
                }
                
                if (crash.Contains(@"DriverModel::DriverModel")) {
                    return WhatsGoingOn.DriverModelIsMissing;
                }
            } catch (Exception e) {
                Logging.Write("[AcLogHelper] Can’t determine what’s going on: " + e);
            }

            return null;
        }
    }
}