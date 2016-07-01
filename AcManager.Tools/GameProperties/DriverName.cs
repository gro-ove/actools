using AcManager.Tools.Helpers;
using AcTools.DataFile;
using AcTools.Processes;

namespace AcManager.Tools.GameProperties {
    public class DriverName : Game.RaceIniProperties {
        public override void Set(IniFile file) {
            var drive = SettingsHolder.Drive;
            if (file["REMOTE"].GetBool("ACTIVE", false)) {
                file["REMOTE"].Set("NAME", drive.DifferentPlayerNameOnline ? drive.PlayerNameOnline : drive.PlayerName);
            } else {
                file["CAR_0"].Set("DRIVER_NAME", drive.PlayerName);
                file["CAR_0"].Set("NATIONALITY", drive.PlayerNationality);
            }
        }
    }
}