using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettings;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.GameProperties {
    public class DriverName : Game.RaceIniProperties {
        public static string GetOnline() {
            var drive = SettingsHolder.Drive;
            return drive.DifferentPlayerNameOnline ? drive.PlayerNameOnline : drive.PlayerName;
        }

        public override void Set(IniFile file) {
            if (file["REMOTE"].GetBool("ACTIVE", false)) {
                var driverName = GetOnline();
                file["REMOTE"].Set("NAME", driverName);
                file["CAR_0"].Set("DRIVER_NAME", driverName);
            } else {
                var drive = SettingsHolder.Drive;
                var playerName = drive.PlayerName;
                if (SettingsHolder.Live.RsrEnabled && SettingsHolder.Live.RsrDifferentPlayerName &&
                        AcSettingsHolder.Forms.Entries.GetByIdOrDefault(RsrMark.FormId)?.IsVisible == true) {
                    playerName = SettingsHolder.Live.RsrPlayerName;
                }

                file["CAR_0"].Set("DRIVER_NAME", playerName);
                file["CAR_0"].Set("NATIONALITY", drive.PlayerNationality);
            }
        }
    }
}