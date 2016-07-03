using AcManager.Tools.Helpers;
using AcTools.DataFile;
using AcTools.Processes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.GameProperties {
    public class DriverName : Game.RaceIniProperties {
        public override void Set(IniFile file) {
            var drive = SettingsHolder.Drive;
            if (file["REMOTE"].GetBool("ACTIVE", false)) {
                file["REMOTE"].Set("NAME", drive.DifferentPlayerNameOnline ? drive.PlayerNameOnline : drive.PlayerName);
            } else {
                var playerName = drive.PlayerName;
                if (SettingsHolder.LiveTiming.RsrEnabled && SettingsHolder.LiveTiming.RsrDifferentPlayerName &&
                        AcSettingsHolder.Forms.Entries.GetByIdOrDefault(RsrMark.FormId)?.IsVisible == true) {
                    Logging.Write("[DriverName] RSR driver name");
                    playerName = SettingsHolder.LiveTiming.RsrPlayerName;
                } else {
                    Logging.Write("[DriverName] Single-player driver name");
                }

                file["CAR_0"].Set("DRIVER_NAME", playerName);
                file["CAR_0"].Set("NATIONALITY", drive.PlayerNationality);
            }
        }
    }
}