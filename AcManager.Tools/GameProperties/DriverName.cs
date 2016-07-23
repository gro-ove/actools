using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.AcSettings;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.GameProperties {
    public class DriverName : Game.RaceIniProperties {
        public override void Set(IniFile file) {
            var drive = SettingsHolder.Drive;
            if (file["REMOTE"].GetBool("ACTIVE", false)) {
                var driverName = drive.DifferentPlayerNameOnline ? drive.PlayerNameOnline : drive.PlayerName;
                file["REMOTE"].Set("NAME", driverName);
                file["CAR_0"].Set("DRIVER_NAME", driverName);
            } else {
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