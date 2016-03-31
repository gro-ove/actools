using System;
using System.IO;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Utils;

namespace AcManager.Tools.AcObjectsNew {
    public class WeatherObjectNew : AcCommonObject {
        public WeatherObjectNew(IFileAcManager manager, string id, bool enabled)
                : base(manager, id, enabled) {}

        private IniFile _iniFile;

        protected override void LoadOrThrow() {
            if (!File.Exists(WeatherIniFilename)) {
                throw new AcErrorException(AcErrorType.Data_IniIsMissing, Path.GetFileName(WeatherIniFilename));
            }

            var text = FileUtils.ReadAllText(WeatherIniFilename);
            
            try {
                _iniFile = IniFile.Parse(text);
            } catch (Exception) {
                _iniFile = null;
                throw new AcErrorException(AcErrorType.Data_IniIsDamaged, Path.GetFileName(WeatherIniFilename));
            }

            Name = _iniFile["LAUNCHER"].Get("NAME");
        }

        public override void Save() {
            _iniFile["LAUNCHER"].Set("NAME", Name);
            _iniFile.Save(WeatherIniFilename);
        }

        public string WeatherIniFilename => Path.Combine(Location, "weather.ini");
    }
}
