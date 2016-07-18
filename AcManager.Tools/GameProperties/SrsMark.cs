using AcManager.Tools.Helpers;
using AcTools.DataFile;
using AcTools.Processes;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.GameProperties {
    public class SrsMark : Game.RaceIniProperties {
        public string Name, Team, Nationality;

        public override void Set(IniFile file) {
            file["REMOTE"].Set("NAME", Name);
            file["REMOTE"].Set("TEAM", Team);
            file["REMOTE"].Set("GUID", SteamIdHelper.Instance.Value ?? "");
            file["CAR_0"].Set("DRIVER_NAME", Name);
            file["CAR_0"].Set("NATIONALITY", Nationality);
            file["CAR_0"].Set("SETUP", "");
            file["CAR_0"].Set("SKIN", "");
            file["CAR_0"].Remove(@"AI_LEVEL");
        }

        private const string KeyName = "srsname";

        public static string GetName() {
            return ValuesStorage.GetString(KeyName, SettingsHolder.Drive.PlayerNameOnline);
        }

        public static void SetName(string name) {
            ValuesStorage.Set(KeyName, name);
        }
    }
}