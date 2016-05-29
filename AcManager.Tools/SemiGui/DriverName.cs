using AcManager.Tools.Helpers;
using AcTools.DataFile;
using AcTools.Processes;

namespace AcManager.Tools.SemiGui {
    public class DriverName : Game.RaceIniProperties {
        public override void Set(IniFile file) {
            var drive = SettingsHolder.Drive;
            var onlineMode = file["REMOTE"].GetBool("ACTIVE", false) && drive.DifferentPlayerNameOnline;
            file["CAR_0"].Set("DRIVER_NAME", onlineMode ? drive.PlayerNameOnline : drive.PlayerName);
            file["CAR_0"].Set("NATIONALITY", drive.PlayerNationality);
        }
    }
}