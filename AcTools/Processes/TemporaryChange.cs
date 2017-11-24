using System;
using System.IO;
using AcTools.DataFile;
using AcTools.Properties;
using AcTools.Utils;

namespace AcTools.Processes {
    internal class VideoIniChange : IDisposable {
        private readonly string _filename, _backup, _originalContent;
        private readonly bool _changed;

        public VideoIniChange(string presetFilename, string ppFilter)
                : this(presetFilename, ppFilter, null, false, false) {}

        public VideoIniChange(string ppFilter, bool? fxaa, bool specialResolution, bool maximizeSettings)
                : this(null, ppFilter, fxaa, specialResolution, maximizeSettings) {}

        private VideoIniChange(string presetFilename, string ppFilter, bool? fxaa, bool specialResolution, bool maximizeSettings) {
            _filename = AcPaths.GetCfgVideoFilename();
            _originalContent = File.ReadAllText(_filename);

            var parseContent = _originalContent;
            if (presetFilename != null && File.Exists(presetFilename)) {
                parseContent = File.ReadAllText(presetFilename);
                _changed = true;
            }

            var video = IniFile.Parse(parseContent);
            if (fxaa.HasValue) {
                video["EFFECTS"].Set("FXAA", fxaa.Value);
                video["POST_PROCESS"].Set("FXAA", fxaa.Value);
                _changed = true;
            }

            if (ppFilter != null) {
                video["POST_PROCESS"].Set("FILTER", ppFilter);
                _changed = true;
            }

            if (specialResolution) {
                video["VIDEO"].Set("FULLSCREEN", false);
                video["VIDEO"].Set("WIDTH", 1920*2);
                video["VIDEO"].Set("HEIGHT", 1080*2);
                _changed = true;
            }

            if (maximizeSettings) {
                video["POST_PROCESS"].Set("ENABLED", true);
                video["POST_PROCESS"].Set("DOF", 0);
                video["POST_PROCESS"].Set("GLARE", 4);
                video["POST_PROCESS"].Set("HEAT_SHIMMER", 0);
                video["POST_PROCESS"].Set("QUALITY", 5);
                video["POST_PROCESS"].Set("RAYS_OF_GOD", 1);
                video["CUBEMAP"].Set("FACES_PER_FRAME", 6);
                video["CUBEMAP"].Set("FARPLANE", 500);
                video["CUBEMAP"].Set("SIZE", 2048);
                _changed = true;
            }

            if (!_changed) return;
            _backup = _filename + ".backup";
            if (File.Exists(_backup)) {
                File.Delete(_backup);
            }
            File.Move(_filename, _backup);
            video.Save(_filename);
        }

        public void Dispose() {
            if (!_changed) return;
            if (File.Exists(_backup)) {
                if (File.Exists(_filename)) {
                    File.Delete(_filename);
                }

                File.Move(_backup, _filename);
            } else {
                File.WriteAllText(_filename, _originalContent);
            }
        }
    }

    internal class ScreenshotFormatChange : IDisposable {
        private readonly string _cfgFile, _originalFormat;

        public ScreenshotFormatChange(string acRoot, string value) {
            _cfgFile = Path.Combine(AcPaths.GetSystemCfgDirectory(acRoot), "assetto_corsa.ini");
            var iniFile = new IniFile(_cfgFile);
            _originalFormat = iniFile["SCREENSHOT"].GetPossiblyEmpty("FORMAT");
            iniFile["SCREENSHOT"].Set("FORMAT", value);
            iniFile.Save();
        }

        public void Dispose() {
            var iniFile = new IniFile(_cfgFile);
            iniFile["SCREENSHOT"].Set("FORMAT", _originalFormat);
            iniFile.Save();
        }
    }

    internal class DisableShowroomWatermarkChange : IDisposable {
        private readonly string _acLogo, _acLogoBackup;

        public DisableShowroomWatermarkChange(string acRoot) {
            _acLogo = AcPaths.GetAcLogoFilename(acRoot);
            if (File.Exists(_acLogo)) {
                _acLogoBackup = _acLogo + "~at_bak";
                if (!File.Exists(_acLogoBackup)) {
                    File.Move(_acLogo, _acLogoBackup);
                }
            } else if (!Directory.Exists(Path.GetDirectoryName(_acLogo) ?? "")) {
                return;
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

    internal class DisableSweetFxChange : IDisposable {
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
