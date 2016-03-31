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
            OnlineConnectionFailed
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


            } catch (Exception e) {
                Logging.Write("[ACLOGHELPER] Can't determine what's going on: " + e);
            }

            return null;
        }
    }
}