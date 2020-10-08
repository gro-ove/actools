using AcManager.Tools.Helpers;
using AcTools.DataFile;
using AcTools.Processes;

namespace AcManager.Tools.GameProperties {
    public class WorldSimSeriesMark : Game.RaceIniProperties {
        public string Name;

        public override void Set(IniFile file) {
            file["REMOTE"].Set("NAME", Name);
            file["REMOTE"].Set("GUID", SteamIdHelper.Instance.Value ?? "");
            file["CAR_0"].Set("DRIVER_NAME", Name);
            file["CAR_0"].Set("SETUP", "");
            file["CAR_0"].Remove(@"AI_LEVEL");
        }
    }
}