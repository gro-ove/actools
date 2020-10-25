using AcManager.Tools.Helpers;
using AcTools.DataFile;
using AcTools.Processes;

namespace AcManager.Tools.GameProperties {
    public class WorldSimSeriesMark : Game.RaceIniProperties {
        public string Name { get; set; }
        public string Nationality { get; set; }
        public string NationCode { get; set; }
        public string Team { get; set; }

        public override void Set(IniFile file) {
            if (Name != null) {
                file["REMOTE"].Set("NAME", Name);
                file["REMOTE"].Set("GUID", SteamIdHelper.Instance.Value ?? "");
                file["CAR_0"].Set("DRIVER_NAME", Name);
                file["CAR_0"].Set("SETUP", "");
                file["CAR_0"].Remove(@"AI_LEVEL");
            }

            if (Nationality != null) {
                file["CAR_0"].Set("NATIONALITY", Nationality);
                file["CAR_0"].Set("NATION_CODE", NationCode ?? NationCodeProvider.Instance.GetNationCode(Nationality));
            }

            if (Team != null) {
                file["REMOTE"].Set("TEAM", Team);
            }
        }
    }
}