using AcTools.DataFile;
using AcTools.Processes;
using JetBrains.Annotations;

namespace AcManager.Tools.GameProperties.WeatherSpecific {
    public class CustomTrackPropertiesHelper : Game.RaceIniProperties {
        [NotNull]
        public string Filename { get; }

        public CustomTrackPropertiesHelper([NotNull] string filename) {
            Filename = filename;
        }

        public override void Set(IniFile file) {
            Game.TrackProperties.Load(new IniFile(Filename)["TRACK_STATE"])?.Set(file);
        }
    }
}