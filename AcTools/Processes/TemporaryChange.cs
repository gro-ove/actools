using System;
using System.IO;
using AcTools.DataFile;
using AcTools.Properties;
using AcTools.Utils;

namespace AcTools.Processes {
    internal interface ITemporaryChange : IDisposable {
    }

    internal class PpFilterChange : ITemporaryChange {
        private readonly string _videoCfgFile, _originalFilter;

        public PpFilterChange(string value) {
            _videoCfgFile = FileUtils.GetCfgVideoFilename();
            var iniFile = new IniFile(_videoCfgFile);
            _originalFilter = iniFile["POST_PROCESS"].Get("FILTER");
            iniFile["POST_PROCESS"].Set("FILTER", value);
            iniFile.Save();
        }

        public void Dispose() {
            var iniFile = new IniFile(_videoCfgFile);
            iniFile["POST_PROCESS"].Set("FILTER", _originalFilter);
            iniFile.Save();
        }
    }

    internal class ScreenshotFormatChange : ITemporaryChange {
        private readonly string _cfgFile, _originalFormat;

        public ScreenshotFormatChange(string acRoot, string value) {
            _cfgFile = Path.Combine(FileUtils.GetSystemCfgDirectory(acRoot), "assetto_corsa.ini");
            var iniFile = new IniFile(_cfgFile);
            _originalFormat = iniFile["SCREENSHOT"].Get("FORMAT");
            iniFile["SCREENSHOT"].Set("FORMAT", value);
            iniFile.Save();
        }

        public void Dispose() {
            var iniFile = new IniFile(_cfgFile);
            iniFile["SCREENSHOT"].Set("FORMAT", _originalFormat);
            iniFile.Save();
        }
    }

    internal class DisableShowroomWatermarkChange : ITemporaryChange {
        private readonly string _acLogo, _acLogoBackup;

        public DisableShowroomWatermarkChange(string acRoot) {
            _acLogo = FileUtils.GetAcLogoFilename(acRoot);
            if (File.Exists(_acLogo)) {
                _acLogoBackup = _acLogo + "~at_bak";
                if (!File.Exists(_acLogoBackup)) {
                    File.Move(_acLogo, _acLogoBackup);
                }
            }

            if (!File.Exists(_acLogo)) {
                Resources.LogoAcApp.Save(_acLogo);
            }
        }

        public void Dispose() {
            if (!File.Exists(_acLogoBackup)) return;
            File.Delete(_acLogo);
            File.Move(_acLogoBackup, _acLogo);
        }
    }

    internal class DisableSweetFxChange : ITemporaryChange {
        private readonly string _acSweetFx, _acSweetFxBackup;

        public DisableSweetFxChange(string acRoot) {
            _acSweetFx = Path.Combine(acRoot, "d3d11.dll");
            if (!File.Exists(_acSweetFx)) {
                _acSweetFx = Path.Combine(acRoot, "dxgi.dll");
            }

            _acSweetFxBackup = _acSweetFx + "~at_bak";
            if (File.Exists(_acSweetFx) && !File.Exists(_acSweetFxBackup)) {
                File.Move(_acSweetFx, _acSweetFxBackup);
            }
        }

        public void Dispose() {
            if (!File.Exists(_acSweetFxBackup)) return;
            File.Delete(_acSweetFx);
            File.Move(_acSweetFxBackup, _acSweetFx);
        }
    }
}
