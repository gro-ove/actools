using System;
using AcTools.Utils;
using System.Diagnostics;
using System.IO;
using AcTools.DataFile;

namespace AcTools.Processes {
    public partial class Showroom {
        public class KunosShotter : AbstractIterableShooter {
            private Process _process;
            private string _originalShowroomFile;

            public void SetCamera(string cameraPosition, string cameraLookAt, double cameraFov, double cameraExposure) {
                var iniFilename = FileUtils.GetCfgShowroomFilename();
                _originalShowroomFile = File.ReadAllText(iniFilename);

                var iniFile = IniFile.Parse(_originalShowroomFile);
                iniFile["PREVIEW_MODE"].Set("USE_CUSTOM_CAMERA", true);
                iniFile["PREVIEW_MODE"].Set("CUSTOM_CAMERA_POSITION", cameraPosition);
                iniFile["PREVIEW_MODE"].Set("CUSTOM_CAMERA_ROLL", false);
                iniFile["PREVIEW_MODE"].Set("CUSTOM_CAMERA_EXPOSURE", false);
                iniFile["PREVIEW_MODE"].Set("LOOK_AT", cameraLookAt);
                iniFile["SETTINGS"].Set("CAMERA_FOV", cameraFov);
                iniFile["SETTINGS"].Set("CAMERA_EXPOSURE", Equals(cameraExposure, 0d) ? 30d : cameraExposure);
                iniFile.Save(iniFilename);
            }

            public override void Shot(string skinId) {
                Prepare();
                PrepareIni(CarId, skinId, ShowroomId);

                _process = Process.Start(new ProcessStartInfo {
                    WorkingDirectory = AcRoot,
                    FileName = "acShowroom.exe",
                    Arguments = "pictureMode",
                    WindowStyle = ProcessWindowStyle.Hidden
                });

                if (_process == null) {
                    throw new Exception("Cannot start showroom");
                }

                _process.WaitForExit();

                var filename = Path.Combine(FileUtils.GetCarSkinDirectory(AcRoot, CarId, skinId),
                        "preview_original.jpg");
                if (!File.Exists(filename)) {
                    throw new Exception("Process exited");
                }

                File.Move(filename, Path.Combine(OutputDirectory, skinId + ".bmp"));
            }

            public override void Dispose() {
                base.Dispose();

                if (_process != null && !_process.HasExited) {
                    _process.Kill();
                    _process = null;
                }

                if (_originalShowroomFile != null) {
                    File.WriteAllText(FileUtils.GetCfgShowroomFilename(), _originalShowroomFile);
                }
            }
        }
    }
}
