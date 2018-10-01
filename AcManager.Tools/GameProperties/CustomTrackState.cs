using AcTools.DataFile;
using AcTools.Processes;

namespace AcManager.Tools.GameProperties {
    public class CustomTrackState : Game.RaceIniProperties {
        public string Filename { get;  }

        public CustomTrackState(string filename) {
            Filename = filename;
        }

        public override void Set(IniFile file) {}
    }
}