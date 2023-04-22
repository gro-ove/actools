using AcTools.DataFile;
using AcTools.Processes;

namespace AcManager.Tools.GameProperties {
    public class NewModeDetails : Game.RaceIniProperties {
        private readonly string _modeId;

        public NewModeDetails(string modeId) {
            _modeId = modeId;
        }

        public override void Set(IniFile file) {
            file["RACE"].Set("__CM_CUSTOM_MODE", _modeId);
        }
    }
}