using System;
using System.IO;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.GameProperties {
    public class WeatherSpecificPpFilterHelper : WeatherSpecificHelperBase {
        private const string FilterId = "__cm_weather";

        private static string Destination => Path.Combine(FileUtils.GetPpFiltersDirectory(AcRootDirectory.Instance.RequireValue), $"{FilterId}.ini");

        public static void Revert() {
            if (AcRootDirectory.Instance.Value == null) return;

            try {
                var destination = Destination;
                if (!File.Exists(destination)) return;

                File.Delete(destination);

                var ini = new IniFile(FileUtils.GetCfgVideoFilename());
                var section = ini["POST_PROCESS"];
                if (section.GetNonEmpty("FILTER") != FilterId) return;

                section.Set("FILTER", section.GetNonEmpty("__OGIRINAL_FILTER") ?? @"default");
                ini.Save();
            } catch (Exception e) {
                Logging.Warning("[WeatherSpecificPpFilterHelper] Revert(): " + e);
            }
        }

        private string _destination, _replacement;

        protected override bool SetOverride(WeatherObject weather) {
            _replacement = Path.Combine(weather.Location, "filter.ini");
            if (!File.Exists(_replacement)) return false;

            _destination = Destination;
            if (File.Exists(_destination)) {
                File.Delete(_destination);
            }
            
            FileUtils.Hardlink(_replacement, _destination);

            var ini = new IniFile(FileUtils.GetCfgVideoFilename());
            var section = ini["POST_PROCESS"];
            section.Set("__OGIRINAL_FILTER", section.GetNonEmpty("FILTER"));
            section.Set("FILTER", FilterId);
            ini.Save();

            return true;
        }

        protected override void DisposeOverride() {
            Revert();
        }
    }
}