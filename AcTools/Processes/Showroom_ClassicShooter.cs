using System;
using AcTools.Utils;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using AcTools.DataFile;
using AcTools.Utils.Helpers;
using AcTools.Windows;
using AcTools.Windows.Input;
using AcTools.Windows.Input.Native;

namespace AcTools.Processes {
    class DistanceChange : IDisposable {
        private readonly string _cfgFile, _originalValue;

        public DistanceChange(double value) {
            _cfgFile = FileUtils.GetCfgShowroomFilename();
            var iniFile = new IniFile(_cfgFile);
            _originalValue = iniFile["SETTINGS"].GetPossiblyEmpty("CAMERA_DISTANCE");
            iniFile["SETTINGS"].Set("CAMERA_DISTANCE", value);
            iniFile.Save();
        }

        public void Dispose() {
            var iniFile = new IniFile(_cfgFile);
            iniFile["SETTINGS"].Set("CAMERA_DISTANCE", _originalValue);
            iniFile.Save();
        }
    }

    class LogActivateChange : IDisposable {
        private readonly string _cfgFile, _originalValue;

        public LogActivateChange(string acRoot) {
            _cfgFile = Path.Combine(FileUtils.GetSystemCfgDirectory(acRoot), "assetto_corsa.ini");
            var iniFile = new IniFile(_cfgFile);
            _originalValue = iniFile["LOG"].GetPossiblyEmpty("SUPPRESS");
            iniFile["LOG"].Set("SUPPRESS", false);
            iniFile.Save();
        }

        public void Dispose() {
            if (_originalValue == "0") return;
            var iniFile = new IniFile(_cfgFile);
            iniFile["LOG"].Set("SUPPRESS", _originalValue);
            iniFile.Save();
        }
    }

    public partial class Showroom {
        public class ClassicShooter : BaseShotter {
            private const int WaitTimeoutPre = 1000;
            private const int WaitTimeoutStep = 200;
            private const int WaitTimeoutShot = 300;
            private const int WaitTimeoutEnsure = 300;
            private const int WaitTimeoutIteration = 10;
            private const int IterationCount = 20;

            private const int DisableRotationClickX = 1272,
                DisableRotationClickY = 1018;

            private string _lastShot;

            private string[] _skins, _shots;
            private double _dx, _dy;
            private bool _slowMode;

            private IDisposable _distanceChange, _logActivateChange;

            private bool _terminated;
            private Process _process;
            private readonly InputSimulator _inputSimulator;

            public ClassicShooter() {
                _inputSimulator = new InputSimulator();
            }

            public void SetRotate(double dx, double dy) {
                _dx = dx;
                _dy = dy;
            }

            public void SetDistance(double distance) {
                DisposeHelper.Dispose(ref _distanceChange);
                _distanceChange = new DistanceChange(distance);
            }

            public void SlowMode() {
                _slowMode = true;
            }

            protected void Run(bool manual) {
                Prepare();
                Showroom();

                if (manual) {
                    // DisableAutorotation();
                    // SendKeys.SendWait("{F7}");
                    _lastShot = WaitShot();
                }

                MakeShots();
                MoveShots();
            }

            public override void Dispose() {
                base.Dispose();

                if (_process != null && !_process.HasExitedSafe()) {
                    _process.Kill();
                    _process = null;
                }

                DisposeHelper.Dispose(ref _distanceChange);
                DisposeHelper.Dispose(ref _logActivateChange);
            }

            protected override void Prepare() {
                base.Prepare();

                _logActivateChange = new LogActivateChange(AcRoot);
                _skins = Directory.GetDirectories(FileUtils.GetCarSkinsDirectory(AcRoot, CarId))
                    .Select(Path.GetFileName).ToArray();
            }

            protected override void Terminate() {
                _terminated = true;
                if (_process != null) {
                    _process.Kill();
                    _process = null;
                }
            }

            public override void ShotAll() {
                Prepare();
                Run(false);
            }

            public void ShotAll(bool manualMode) {
                Prepare();
                Run(manualMode);
            }

            private void Showroom() {
                PrepareIni(CarId, _skins[0], ShowroomId);

                _process = Process.Start(new ProcessStartInfo() {
                    WorkingDirectory = AcRoot,
                    FileName = "acShowroom.exe"
                });

                _shots = new string[_skins.Length];

                Wait(WaitTimeoutPre);
                PressKey(Keys.F7);

                LoadingWait();
                Wait(WaitTimeoutPre);

                if (!Equals(_dx, 0.0) || !Equals(_dy, 0.0)) {
                    RotateCam(_dx, _dy);
                }
            }

            private void LoadingWait() {
                PressKey(Keys.F8);
                var shot = WaitShot();
                if (shot != null) {
                    File.Delete(shot);
                }

                Wait(WaitTimeoutPre);
            }

            private void MakeShots() {
                for (var i = 0; i < _skins.Length; i++) {
                    if (_slowMode) Wait(WaitTimeoutEnsure);
                    if (i > 0) NextSkin();
                    if (_slowMode) Wait(WaitTimeoutEnsure);

                    if (_lastShot != null) {
                        _shots[i] = _lastShot;
                        _lastShot = null;
                    } else {
                        _shots[i] = MakeShot();
                    }
                }
            }

            private void MoveShots() {
                for (var i = 0; i < _skins.Length; i++) {
                    File.Move(_shots[i], Path.Combine(OutputDirectory, _skins[i] + ".bmp"));
                }
            }

            // ReSharper disable once UnusedMember.Local
            private void DisableAutorotation() {
                User32.BringProcessWindowToFront(_process);
                _inputSimulator.Mouse.MoveMouseTo((int)(65536.0 * DisableRotationClickX / Screen.PrimaryScreen.Bounds.Width),
                    (int)(65536.0 * DisableRotationClickY / Screen.PrimaryScreen.Bounds.Height));
                _inputSimulator.Mouse.LeftButtonClick();
            }

            private void RotateCam(double x, double y) {
                PressKey(Keys.F7);

                User32.BringProcessWindowToFront(_process);
                _inputSimulator.Mouse.MoveMouseTo(32767 - 32767 * x / Screen.PrimaryScreen.Bounds.Width,
                    32767 - 32767 * y / Screen.PrimaryScreen.Bounds.Height);
                _inputSimulator.Mouse.RightButtonDown();
                Wait(WaitTimeoutIteration);

                var lx = 0.0;
                var ly = 0.0;
                var px = 0;
                var py = 0;

                for (var i = 0; i < IterationCount; i++) {
                    lx += x / IterationCount;
                    ly += y / IterationCount;

                    var dx = (int)Math.Round(lx) - px;
                    var dy = (int)Math.Round(ly) - py;

                    User32.BringProcessWindowToFront(_process);
                    _inputSimulator.Mouse.MoveMouseBy(dx, dy);
                    px += dx;
                    py += dy;

                    Wait(WaitTimeoutIteration);
                }

                User32.BringProcessWindowToFront(_process);
                _inputSimulator.Mouse.RightButtonUp();
                PressKey(Keys.F7);
                Wait(WaitTimeoutEnsure);
            }

            private string MakeShot() {
                string result;
                do {
                    var from = DateTime.Now;
                    PressKey(Keys.F8);
                    result = WaitShot(from, WaitTimeoutShot);
                } while (result == null);

                return result;
            }

            private string WaitShot(DateTime? from = null, int time = int.MaxValue) {
                if (from == null) {
                    from = DateTime.Now;
                }

                for (; time > 0; time -= WaitTimeoutStep) {
                    var files = Directory.GetFiles(FileUtils.GetDocumentsScreensDirectory(), "Showroom_" + CarId + "_*.bmp")
                        .Where(x => new FileInfo(x).CreationTime > from).ToList();
                    if (files.Any()) {
                        Wait(WaitTimeoutEnsure);
                        return files[0];
                    }

                    Wait(WaitTimeoutStep);
                }

                return null;
            }

            private void NextSkin() {
                PressKey(Keys.PageDown);
                Wait(WaitTimeoutEnsure);
            }

            private void PressKey(Keys key) {
                User32.BringProcessWindowToFront(_process);
                var code = (VirtualKeyCode)key;
                _inputSimulator.Keyboard.KeyDown(code);
                Wait(WaitTimeoutIteration);
                _inputSimulator.Keyboard.KeyUp(code);
                Wait(WaitTimeoutIteration);
            }

            private void Wait(int delay) {
                Thread.Sleep(delay);
                if (_terminated) {
                    throw new ShotingCancelledException();
                }

                if (_process.HasExitedSafe()) {
                    throw new ProcessExitedException();
                }
            }
        }

        public class ClassisManualShooter : ClassicShooter {
            public override void ShotAll() {
                Prepare();
                Run(true);
            }
        }
    }
}
