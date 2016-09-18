using AcTools.Utils;
using System.Diagnostics;
using System.IO;
using AcTools.DataFile;

namespace AcTools.Processes {
    public partial class Showroom {
        private static void PrepareIni(string carId, string carSkinId, string showroomId) {
            var filename = FileUtils.GetCfgShowroomFilename();
            var iniFile = new IniFile(filename);
            iniFile["SHOWROOM"].Set("CAR", (carId ?? "").ToLowerInvariant());
            iniFile["SHOWROOM"].Set("SKIN", (carSkinId ?? "").ToLowerInvariant());
            iniFile["SHOWROOM"].Set("TRACK", (showroomId ?? "").ToLowerInvariant());
            iniFile.Save();
        }
        
        private static string GetCurrentFilterIni() {
            return new IniFile(FileUtils.GetCfgVideoFilename())["POST_PROCESS"].GetPossiblyEmpty("FILTER");
        }
        
        private static void SetCurrentFilterIni(string filter) {
            var filename = FileUtils.GetCfgVideoFilename();
            var iniFile = new IniFile(filename);
            iniFile["POST_PROCESS"].Set("FILTER", filter);
            iniFile.Save();
        }

        public class ShowroomProperties {
            public string AcRoot;
            public string ShowroomId, CarId, CarSkinId;

            public double CameraFov;

            public string Filter;
            public bool UseBmp = false,
                DisableWatermark = false,
                DisableSweetFx = false;
        }

        private static bool _busy;

        public static void Start(ShowroomProperties properties) {
            if (_busy) return;
            _busy = true;

            try {
                var filename = FileUtils.GetCfgShowroomFilename();
                var originalShowroomFile = File.ReadAllText(filename);

                var iniFile = IniFile.Parse(originalShowroomFile);
                iniFile["SHOWROOM"].Set("CAR", (properties.CarId ?? "").ToLowerInvariant());
                iniFile["SHOWROOM"].Set("SKIN", (properties.CarSkinId ?? "").ToLowerInvariant());
                iniFile["SHOWROOM"].Set("TRACK", (properties.ShowroomId ?? "").ToLowerInvariant());
                iniFile["SETTINGS"].Set("CAMERA_FOV", properties.CameraFov);
                iniFile.Save(filename);

                using (properties.UseBmp ? new ScreenshotFormatChange(properties.AcRoot, "BMP") : null)
                using (properties.DisableWatermark ? new DisableShowroomWatermarkChange(properties.AcRoot) : null)
                using (properties.DisableSweetFx ? new DisableSweetFxChange(properties.AcRoot) : null)
                using (properties.Filter != null ? new VideoIniChange(properties.Filter, null, false, false) : null) {
                    var process = Process.Start(new ProcessStartInfo {
                        WorkingDirectory = properties.AcRoot,
                        FileName = "acShowroom.exe"
                    });

                    process?.WaitForExit();
                }
            } finally {
                _busy = false;
            }
        }
    }
}
