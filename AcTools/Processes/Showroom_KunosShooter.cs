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

                new IniFile {
                    ["SHOWROOM"] = {
                        ["ALLOW_SELECT_SKIN"] = true,
                        ["SELECTED_SKIN"] = 1,
                        ["CAR_ID"] = 0
                    },
                    ["FADES"] = {
                        ["ENTER_EXIT_MS"] = 0
                    },
                    ["PREVIEW_MODE"] = {
                        ["LOOK_AT"] = cameraLookAt,
                        ["CUSTOM_CAMERA_POSITION"] = cameraPosition,
                        ["USE_CUSTOM_CAMERA"] = true,
                        ["CUSTOM_CAMERA_ROLL"] = 0.0,
                        ["CUSTOM_CAMERA_EXPOSURE"] = cameraExposure
                    },
                    ["ANIMATION"] = {
                        ["MUL"] = 0.15
                    },
                    ["SETTINGS"] = {
                        ["ROTATION_SPEED"] = 0.0,
                        ["CAMERA_DISTANCE"] = 6.0,
                        ["CAMERA_HEIGHT"] = 1.5,
                        ["CAMERA_FOV"] = cameraFov,
                        ["CAMERA_EXPOSURE"] = 30.0,
                        ["SUN_ANGLE"] = -50.0,
                        ["SHADOW_SPLIT0"] = 2.0,
                        ["SHADOW_SPLIT1"] = 12.0,
                        ["SHADOW_SPLIT2"] = 50.0,
                        ["NEAR_PLANE"] = 0.01,
                        ["FAR_PLANE"] = 200,
                        ["MIN_EXPOSURE"] = 0.2,
                        ["MAX_EXPOSURE"] = 10000
                    }
                }.Save(iniFilename);
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
