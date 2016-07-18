using System;
using System.IO;
using System.Linq;
using AcTools.Utils;
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
        }

        public static WhatsGoingOn? TryToDetermineWhatsGoingOn() {
            try {
                // TODO: file could be busy
                var log = File.ReadAllLines(FileUtils.GetLogFilename());

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
                
                if (log.SkipWhile(x => x != @"CRASH in:").Any(x => x.Contains(@"DriverModel::DriverModel"))) {
                    return WhatsGoingOn.DriverModelIsMissing;
                }
            } catch (Exception e) {
                Logging.Write("[AcLogHelper] Can’t determine what’s going on: " + e);
            }

            return null;
        }
    }
}