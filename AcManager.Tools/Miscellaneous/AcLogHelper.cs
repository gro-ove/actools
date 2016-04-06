using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Miscellaneous {
    public static class AcLogHelper {
        public enum WhatsGoingOn {
            [Description("Password is invalid")]
            OnlineWrongPassword,

            [Description("Can't connect to remote server")]
            OnlineConnectionFailed,

            [Description("Suspension objects (SUSP_LF, SUSP_LR, …) are missing")]
            SuspensionIsMissing,

            [Description("Wheels objects (WHEEL_LF, WHEEL_LR, …) are missing")]
            WheelsAreMissing,
        }

        public static WhatsGoingOn? TryToDetermineWhatsGoingOn() {
            try {
                var log = File.ReadAllLines(FileUtils.GetLogFilename());

                if (log.Contains("ACClient:: ACP_WRONG_PASSWORD")) {
                    return WhatsGoingOn.OnlineWrongPassword;
                }

                if (log.Contains("ERROR: RaceManager :: Handshake FAILED")) {
                    return WhatsGoingOn.OnlineConnectionFailed;
                }
                
                if (log.Contains("COULD NOT FIND SUSPENSION OBJECT SUSP_")) {
                    return WhatsGoingOn.SuspensionIsMissing;
                }
                
                if (log.Contains("COULD NOT FIND SUSPENSION OBJECT WHEEL_")) {
                    return WhatsGoingOn.WheelsAreMissing;
                }
            } catch (Exception e) {
                Logging.Write("[ACLOGHELPER] Can't determine what's going on: " + e);
            }

            return null;
        }
    }
}