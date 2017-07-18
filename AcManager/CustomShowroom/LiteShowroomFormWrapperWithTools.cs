using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Plugins;
using AcManager.Tools.Objects;
using AcTools.Render.Base;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Render.Kn5SpecificForwardDark;
using AcTools.Render.Special;
using AcTools.Render.Wrapper;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.Windows;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using SlimDX;
using KeyEventArgs = System.Windows.Forms.KeyEventArgs;
using Size = System.Drawing.Size;

namespace AcManager.CustomShowroom {
    public class LiteShowroomFormWrapperWithTools : LiteShowroomFormWrapper {
        private readonly AttachedHelper _helper;
        private readonly LiteShowroomTools _tools;

        public new ToolsKn5ObjectRenderer Kn5ObjectRenderer => (ToolsKn5ObjectRenderer)Renderer;

        public LiteShowroomFormWrapperWithTools(ToolsKn5ObjectRenderer renderer, CarObject car, string skinId, string presetFilename)
                : base(renderer, car.DisplayName) {
            _tools = new LiteShowroomTools(renderer, car, skinId, presetFilename);
            _helper = new AttachedHelper(this, _tools, limitHeight: false);
            GoToNormalMode();

            renderer.VisibleUi = false;
            Form.Move += OnMove;
        }

        protected override void OnClick() {
            if (_busy) return;
            base.OnClick();
            if (_tools.CanSelectNodes && !User32.IsKeyPressed(Keys.LControlKey) && !User32.IsKeyPressed(Keys.RControlKey)) {
                Kn5ObjectRenderer.OnClick(new Vector2(MousePosition.X, MousePosition.Y));
            }
        }

        protected override void OnMouseWheel(object sender, MouseEventArgs e) {
            if (_busy) return;
            base.OnMouseWheel(sender, e);
        }

        protected override void OnTick(object sender, TickEventArgs args) {
            if (_busy) return;
            base.OnTick(sender, args);
        }

        private bool _switchingInProgress;

        private const string KeyNormalMaximized = "_LiteShowroomWrapperWithTools.NormalMaximized";
        private const string KeyNormalFullscreen = "_LiteShowroomWrapperWithTools.NormalFullscreen";
        private const string KeyNormalSize = "_LiteShowroomWrapperWithTools.NormalSize";
        private const string KeyNormalPos = "_LiteShowroomWrapperWithTools.NormalPos";

        private const string KeyToolSize = "_LiteShowroomWrapperWithTools.ToolSize";
        private const string KeyToolPos = "_LiteShowroomWrapperWithTools.ToolPos";

        private bool? _lastVisibleTools;

        protected sealed override void GoToNormalMode() {
            _switchingInProgress = true;

            try {
                var area = Screen.PrimaryScreen.WorkingArea;
                var size = ValuesStorage.GetPoint(KeyNormalSize, new Point(1600, 900));
                var pos = ValuesStorage.GetPoint(KeyNormalPos, new Point((area.Width - size.X) / 2, (area.Height - size.Y) / 2));

                Form.Width = ((int)size.X).Clamp(320, area.Width);
                Form.Height = ((int)size.Y).Clamp(200, area.Height);
                Form.Top = ((int)pos.Y).Clamp(0, area.Height - Form.Height);
                Form.Left = ((int)pos.X).Clamp(0, area.Width - Form.Width);

                Form.WindowState = ValuesStorage.GetBool(KeyNormalMaximized) ? FormWindowState.Maximized : FormWindowState.Normal;
                Form.FormBorderStyle = FormBorderStyle.Sizable;
                Form.TopMost = false;
                FullscreenEnabled = ValuesStorage.GetBool(KeyNormalFullscreen);

                UpdateSize();

                if (_lastVisibleTools.HasValue) {
                    _helper.Visible = _lastVisibleTools.Value;
                }
            } finally {
                _switchingInProgress = false;
            }
        }

        protected override void GoToToolMode() {
            _switchingInProgress = true;

            try {
                var area = Screen.PrimaryScreen.WorkingArea;
                var size = ValuesStorage.GetPoint(KeyToolSize, new Point(400, 240));
                var pos = ValuesStorage.GetPoint(KeyToolPos, new Point(80, Screen.PrimaryScreen.WorkingArea.Height - 300));

                _lastVisibleTools = _helper.Visible;
                _helper.Visible = false;

                FullscreenEnabled = false;
                Form.WindowState = FormWindowState.Normal;
                Form.Width = ((int)size.X).Clamp(320, area.Width);
                Form.Height = ((int)size.Y).Clamp(200, area.Height);
                Form.Top = ((int)pos.Y).Clamp(0, area.Height - Form.Height);
                Form.Left = ((int)pos.X).Clamp(0, area.Width - Form.Width);

                Form.FormBorderStyle = FormBorderStyle.SizableToolWindow;
                Form.TopMost = true;

                UpdateSize();
            } finally {
                _switchingInProgress = false;
            }
        }

        private void Save() {
            if (_switchingInProgress) return;

            if (EditMode) {
                ValuesStorage.Set(KeyToolSize, new Point(Form.Width, Form.Height));
                ValuesStorage.Set(KeyToolPos, new Point(Form.Left, Form.Top));
            } else {
                ValuesStorage.Set(KeyNormalFullscreen, FullscreenEnabled);

                if (FullscreenEnabled) {
                    ValuesStorage.Set(KeyNormalMaximized, false);
                } else {
                    ValuesStorage.Set(KeyNormalMaximized, Form.WindowState == FormWindowState.Maximized);
                    if (Form.WindowState == FormWindowState.Normal) {
                        ValuesStorage.Set(KeyNormalSize, new Point(Form.Width, Form.Height));
                        ValuesStorage.Set(KeyNormalPos, new Point(Form.Left, Form.Top));
                    }
                }
            }
        }

        private bool _busy;

        protected override void UpdateSize() {
            if (_busy) return;
            base.UpdateSize();
        }

        private async void ShotAsync(Action<IProgress<Tuple<string, double?>>, CancellationToken> action) {
            if (_busy) return;
            _busy = true;

            try {
                using (var waiting = new WaitingDialog {
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Owner = null
                }) {
                    waiting.Report(AsyncProgressEntry.Indetermitate);

                    var cancellation = waiting.CancellationToken;
                    Renderer.IsPaused = true;

                    try {
                        await Task.Run(() => {
                            // ReSharper disable once AccessToDisposedClosure
                            action(waiting, cancellation);
                        });
                    } finally {
                        Renderer.IsPaused = false;
                    }
                }
            } catch (Exception e) {
                NonfatalError.Notify("Can’t build image", e);
            } finally {
                _busy = false;
                UpdateSize();
            }
        }

        private static bool _warningShown;

        protected override void SplitShotPieces(Size size, bool downscale, string filename, IProgress<Tuple<string, double?>> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            PiecesBlender.OptionMaxCacheSize = SettingsHolder.Plugins.MontageVramCache;

            var plugin = PluginsManager.Instance.GetById("ImageMontage");
            if (plugin == null || !plugin.IsReady) {
                if (!_warningShown) {
                    _warningShown = true;
                    FirstFloor.ModernUI.Windows.Toast.Show("Montage Plugin Not Installed", "You’ll have to join pieces manually");
                }

                OptionMontageMemoryLimit = SettingsHolder.Plugins.MontageMemoryLimit;
                base.SplitShotPieces(size, downscale, filename, progress, cancellation);
            } else {
                var dark = (DarkKn5ObjectRenderer)Renderer;
                var destination = Path.Combine(SettingsHolder.Plugins.MontageTemporaryDirectory, Path.GetFileNameWithoutExtension(filename) ?? "image");

                // For pre-smoothed files, in case somebody would want to use super-resolution with SSLR/SSAO
                DarkKn5ObjectRenderer.OptionTemporaryDirectory = destination;

                var information = dark.SplitShot(size.Width, size.Height, downscale ? 0.5d : 1d, destination, progress.SubrangeTuple(0.001, 0.95, "Rendering ({0})…"), cancellation);

                progress?.Report(new Tuple<string, double?>("Combining pieces…", 0.97));

                var magick = plugin.GetFilename("magick.exe");
                if (!File.Exists(magick)) {
                    magick = plugin.GetFilename("montage.exe");
                    FirstFloor.ModernUI.Windows.Toast.Show("Montage Plugin Is Obsolete", "Please, update it, and it’ll consume twice less power");
                }

                Environment.SetEnvironmentVariable("MAGICK_TMPDIR", destination);
                using (var process = new Process {
                    StartInfo = {
                    FileName = magick,
                    WorkingDirectory = destination,
                    Arguments = $"montage piece-*-*.{information.Extension} -limit memory {SettingsHolder.Plugins.MontageMemoryLimit.ToInvariantString()} -limit map {SettingsHolder.Plugins.MontageMemoryLimit.ToInvariantString()} -tile {information.Cuts.ToInvariantString()}x{information.Cuts.ToInvariantString()} -geometry +0+0 out.jpg",
                    CreateNoWindow = true,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                },
                    EnableRaisingEvents = true
                }) {
                    process.Start();
                    process.WaitForExit(600000);
                    if (!process.HasExited) {
                        process.Kill();
                    }
                }

                progress?.Report(new Tuple<string, double?>("Cleaning up…", 0.99));

                var result = Path.Combine(destination, "out.jpg");
                if (!File.Exists(result)) {
                    throw new Exception("Combining failed, file not found");
                }

                File.Move(result, filename);
                Directory.Delete(destination, true);
            }
        }

        protected override void SplitShot(Size size, bool downscale, string filename) {
            ShotAsync((progress, token) => {
                SplitShotInner(size, downscale, filename, progress, token);
            });
        }

        protected override void Shot(Size size, bool downscale, string filename) {
            ShotAsync((progress, token) => {
                ShotInner(size, downscale, filename, progress, token);
            });
        }

        protected override void OnKeyUp(object sender, KeyEventArgs args) {
            if (_busy) return;

            switch (args.KeyCode) {
                case Keys.H:
                    if (args.Alt) {
                        var tools = Renderer as ToolsKn5ObjectRenderer;
                        if (tools != null) {
                            if (!args.Control && !args.Shift) {
                                tools.ToggleSelected();
                            } else if (!args.Control && args.Shift) {
                                tools.UnhideAll();
                            }
                        }
                    } else if (args.Control && !args.Shift) {
                        _helper.Visible = !_helper.Visible;
                        args.Handled = true;
                    }
                    break;

                case Keys.D:
                    if (args.Control && !args.Alt && !args.Shift) {
                        Kn5ObjectRenderer.Deselect();
                    }
                    break;

                case Keys.Escape:
                    if (Kn5ObjectRenderer.SelectedObject != null) {
                        Kn5ObjectRenderer.Deselect();
                        args.Handled = true;
                        return;
                    }
                    break;

                case Keys.Space:
                    if (args.Control && !args.Alt && !args.Shift) {
                        Kn5ObjectRenderer.AutoRotate = !Kn5ObjectRenderer.AutoRotate;
                        args.Handled = true;
                    }
                    break;

                case Keys.Tab:
                    if (!args.Control && !args.Alt && !args.Shift) {
                        args.Handled = true;
                    }

                    if (args.Control && !args.Alt && args.Shift) {
                        Renderer.SyncInterval = !Renderer.SyncInterval;
                    }
                    break;
            }


            base.OnKeyUp(sender, args);
        }

        protected override bool SleepMode => base.SleepMode && !_helper.IsActive;

        protected override void OnResize(object sender, EventArgs e) {
            base.OnResize(sender, e);
            Save();
        }

        protected void OnMove(object sender, EventArgs e) {
            Save();
        }

        protected override void OnFullscreenChanged() {
            base.OnFullscreenChanged();
            Save();
        }
    }
}