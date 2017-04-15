// #define SSLR_PARAMETRIZED

using System;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Forms;
using AcTools.Render.Base;
using AcTools.Render.Forward;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Render.Kn5SpecificForwardDark;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.Windows;
using SlimDX;

namespace AcTools.Render.Wrapper {
    public class LiteShowroomWrapper : BaseKn5FormWrapper {
        public bool OptionHwDownscale = true;

        private readonly ForwardKn5ObjectRenderer _renderer;

        public bool ReplaceableShowroom { get; set; }

        private bool _editMode;

        public bool EditMode {
            get { return _editMode; }
            set {
                if (Equals(value, _editMode)) return;
                _editMode = value;

                if (value) {
                    GoToToolMode();
                } else {
                    GoToNormalMode();
                }
            }
        }

        public LiteShowroomWrapper(ForwardKn5ObjectRenderer renderer, string title = "Lite Showroom", int width = 1600, int height = 900)
                : base(renderer, title, width, height) {
            Form.MouseDoubleClick += OnMouseDoubleClick;
            _renderer = renderer;

#if SSLR_PARAMETRIZED
            _renderer.UseSprite = true;
            _renderer.VisibleUi = true;
#endif
        }

        private void OnMouseDoubleClick(object sender, MouseEventArgs e) {
            _renderer.AutoAdjustTarget = true;
        }

        protected virtual void GoToNormalMode() {
            Form.FormBorderStyle = FormBorderStyle.Sizable;
            Form.Width = 1600;
            Form.Height = 900;
            Form.Left = (Screen.PrimaryScreen.WorkingArea.Width - Form.Width) / 2;
            Form.Top = (Screen.PrimaryScreen.WorkingArea.Height - Form.Height) / 2;
            Form.TopMost = false;
            Kn5ObjectRenderer.VisibleUi = true;
            UpdateSize();
        }

        protected virtual void GoToToolMode() {
            Form.FormBorderStyle = FormBorderStyle.SizableToolWindow;
            Form.Width = 400;
            Form.Height = 240;
            Form.Top = Screen.PrimaryScreen.WorkingArea.Height - 300;
            Form.Left = 80;
            Form.TopMost = true;
            Kn5ObjectRenderer.VisibleUi = false;
            UpdateSize();
        }

        private static readonly Vector2[] Directions = {
            new Vector2(-0.7071f, -0.7071f),
            new Vector2(0, -1f),
            new Vector2(0.7071f, -0.7071f),
            new Vector2(-1f, 0),
            new Vector2(0, 0),
            new Vector2(1f, 0),
            new Vector2(-0.7071f, 0.7071f),
            new Vector2(0, 1f),
            new Vector2(0.7071f, 0.7071f),
        };

        protected override void OnTick(object sender, TickEventArgs args) {
            if (!Form.Focused) return;

#if SSLR_PARAMETRIZED
            var sslrSetupMode = (_renderer as DarkKn5ObjectRenderer)?.SslrAdjustCurrentMode > DarkKn5ObjectRenderer.SslrAdjustMode.None;
            if (!sslrSetupMode) {
                base.OnTick(sender, args);
            }
#else
            base.OnTick(sender, args);
#endif

            if (User32.IsKeyPressed(Keys.LMenu) || User32.IsKeyPressed(Keys.RMenu)) {
                if (_renderer.CarNode != null) {
                    var steeringSpeed = (User32.IsKeyPressed(Keys.LShiftKey) ? 3f : 30f) * args.DeltaTime;

                    if (User32.IsKeyPressed(Keys.Left)) {
                        _renderer.CarNode.SteerDeg = (_renderer.CarNode.SteerDeg - steeringSpeed).Clamp(-30f, 30f);
                    }

                    if (User32.IsKeyPressed(Keys.Right)) {
                        _renderer.CarNode.SteerDeg = (_renderer.CarNode.SteerDeg + steeringSpeed).Clamp(-30f, 30f);
                    }
                }

                var renderer = _renderer as DarkKn5ObjectRenderer;
                if (renderer != null) {
                    if (User32.IsKeyPressed(Keys.Up)) {
                        renderer.FlatMirrorReflectiveness += (1f - renderer.FlatMirrorReflectiveness) / 12f;
                    }

                    if (User32.IsKeyPressed(Keys.Down)) {
                        renderer.FlatMirrorReflectiveness += (0f - renderer.FlatMirrorReflectiveness) / 12f;
                    }

                    var offset = Vector2.Zero;
                    var offsetCount = 0;

                    for (var i = Keys.NumPad1; i <= Keys.NumPad9; i++) {
                        if (User32.IsKeyPressed(i)) {
                            offset += (Directions[i - Keys.NumPad1] + offset * offsetCount) / ++offsetCount;
                        }
                    }

                    if (offsetCount > 0) {
                        var right = Vector3.Cross(renderer.Light, Vector3.UnitY);
                        var up = Vector3.Cross(renderer.Light, right);
                        var upd = renderer.Light + (up * offset.Y + right * offset.X) * args.DeltaTime;
                        renderer.Light = upd;
                    }
                }
            }

#if SSLR_PARAMETRIZED
            if (sslrSetupMode && (User32.IsKeyPressed(Keys.LControlKey) || User32.IsKeyPressed(Keys.RControlKey))) {
                var renderer = _renderer as DarkKn5ObjectRenderer;
                if (renderer != null) {
                    var delta = (User32.IsKeyPressed(Keys.LShiftKey) ? 0.01f : 0.1f) * args.DeltaTime;

                    if (User32.IsKeyPressed(Keys.Up)) {
                        renderer.SslrAdjust(delta);
                    }

                    if (User32.IsKeyPressed(Keys.Down)) {
                        renderer.SslrAdjust(-delta);
                    }
                }
            }
#endif

            if (User32.IsKeyPressed(Keys.LControlKey) || User32.IsKeyPressed(Keys.RControlKey)) {
                var speed = (User32.IsKeyPressed(Keys.LShiftKey) ? 0.05f : 0.5f) * args.DeltaTime;

                if (User32.IsKeyPressed(Keys.NumPad7)) {
                    _renderer.ToneExposure += speed;
                }

                if (User32.IsKeyPressed(Keys.NumPad4)) {
                    _renderer.ToneExposure -= speed;
                }

                if (User32.IsKeyPressed(Keys.NumPad8)) {
                    _renderer.ToneGamma += speed;
                }

                if (User32.IsKeyPressed(Keys.NumPad5)) {
                    _renderer.ToneGamma -= speed;
                }

                if (User32.IsKeyPressed(Keys.NumPad9)) {
                    _renderer.ToneWhitePoint += speed;
                }

                if (User32.IsKeyPressed(Keys.NumPad6)) {
                    _renderer.ToneWhitePoint -= speed;
                }
            }
        }

        private class ProgressWrapper : IProgress<double> {
            private IProgress<Tuple<string, double?>> _main;
            private readonly string _message;

            public ProgressWrapper(IProgress<Tuple<string, double?>> main, string message) {
                _main = main;
                _message = message;
            }

            public void Report(double value) {
                _main.Report(Tuple.Create(_message, (double?)value));
            }
        }

        protected static IProgress<double> Wrap(IProgress<Tuple<string, double?>> baseProgress, string message = null) {
            return new ProgressWrapper(baseProgress, message ?? "Rendering…");
        }

        protected virtual void SplitShotPieces(double multipler, bool downscale, string filename, IProgress<Tuple<string, double?>> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            var dark = (DarkKn5ObjectRenderer)Renderer;
            var destination = filename.ApartFromLast(".jpg", StringComparison.OrdinalIgnoreCase);
            var information = dark.SplitShot(multipler, OptionHwDownscale && downscale ? 0.5d : 1d, destination,
                    !OptionHwDownscale && downscale, Wrap(progress), cancellation);
            File.WriteAllText(Path.Combine(destination, "join.bat"), $@"@echo off
rem Use montage.exe from ImageMagick for Windows to run this script 
rem and combine images: https://www.imagemagick.org/script/binary-releases.php
montage.exe *-*.{information.Extension} -tile {information.Cuts}x{information.Cuts} -geometry +0+0 out.jpg
echo @del *-*.{information.Extension} delete-pieces.bat join.bat > delete-pieces.bat");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        protected void SplitShotInner(double multipler, bool downscale, string filename, IProgress<Tuple<string, double?>> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            if (multipler > 4d) {
                SplitShotPieces(multipler, downscale, filename, progress, cancellation);
            } else {
                var dark = (DarkKn5ObjectRenderer)Renderer;
                using (var image = dark.SplitShot(multipler, OptionHwDownscale && downscale ? 0.5d : 1d, Wrap(progress), cancellation)) {
                    if (cancellation.IsCancellationRequested) return;

                    if (downscale && !OptionHwDownscale) {
                        progress?.Report(Tuple.Create("Downscaling…", (double?)0.93));
                        image.Downscale();
                        if (cancellation.IsCancellationRequested) return;
                    }

                    progress?.Report(Tuple.Create("Saving…", (double?)0.95));
                    ImageUtils.SaveImage(image, filename, 95, new ImageUtils.ImageInformation());
                }
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        protected void ShotInner(double multipler, bool downscale, string filename, IProgress<Tuple<string, double?>> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            using (var stream = new MemoryStream()) {
                progress?.Report(Tuple.Create("Rendering…", (double?)0.2));
                _renderer.Shot(multipler, OptionHwDownscale && downscale ? 0.5 : 1d, stream, true);
                stream.Position = 0;
                if (cancellation.IsCancellationRequested) return;

                using (var destination = File.Open(filename, FileMode.Create, FileAccess.ReadWrite)) {
                    progress?.Report(Tuple.Create("Saving…", (double?)0.6));
                    ImageUtils.Convert(stream, destination, !OptionHwDownscale && downscale
                            ? new Size((_renderer.ActualWidth * multipler / 2).RoundToInt(), (_renderer.ActualHeight * multipler / 2).RoundToInt())
                            : (Size?)null, 95, new ImageUtils.ImageInformation());
                }
            }
        }
        
        protected virtual void SplitShot(double multipler, bool downscale, string filename) {
            SplitShotInner(multipler, downscale, filename);
        }
        
        protected virtual void Shot(double multipler, bool downscale, string filename) {
            ShotInner(multipler, downscale, filename);
        }

        private void Shot() {
            var splitMode = ImageUtils.IsMagickAsseblyLoaded && Renderer is DarkKn5ObjectRenderer;

            // hold shift to disable downsampling
            // hold ctrl to render scene in 6x resolution
            // hold alt to render scene in 4x resolution
            // hold both for 1x only

            var ctrlPressed = User32.IsKeyPressed(Keys.LControlKey) || User32.IsKeyPressed(Keys.RControlKey);
            var altPressed = User32.IsKeyPressed(Keys.LMenu) || User32.IsKeyPressed(Keys.RMenu);
            var shiftPressed = User32.IsKeyPressed(Keys.LShiftKey) || User32.IsKeyPressed(Keys.RShiftKey);
            var winPressed = User32.IsKeyPressed(Keys.LWin) || User32.IsKeyPressed(Keys.RWin);

            var downscale = !shiftPressed;
            double multipler;
            if (winPressed && splitMode) {
                multipler = altPressed ? ctrlPressed ? 16d : 12d : ctrlPressed ? 10d : 6d;
            } else if (ctrlPressed) {
                multipler = altPressed ? 1d : splitMode ? 8d : 4d;
            } else if (altPressed) {
                multipler = splitMode ? 4d : 3d;
            } else {
                multipler = 2d;
            }

            _renderer.KeepFxaaWhileShooting = !downscale;

            var directory = FileUtils.GetDocumentsScreensDirectory();
            FileUtils.EnsureDirectoryExists(directory);
            var filename = Path.Combine(directory, $"__custom_showroom_{DateTime.Now.ToUnixTimestamp()}.jpg");

            if (splitMode && multipler > 2d) {
                SplitShot(multipler, downscale, filename);
            } else {
                Shot(multipler, downscale, filename);
            }
        }

        protected override void OnKeyUp(object sender, KeyEventArgs args) {
            if (args.KeyCode == Keys.F2 && !args.Control && !args.Alt && !args.Shift || args.KeyCode == Keys.Escape && EditMode) {
                EditMode = !EditMode;
                return;
            }

            if (args.KeyCode == Keys.Space && !args.Control && !args.Alt && !args.Shift) {
                return;
            }

            base.OnKeyUp(sender, args);
            if (args.Handled) return;

            switch (args.KeyCode) {
#if SSLR_PARAMETRIZED
                case Keys.D1:
                    if (!args.Control && !args.Alt && !args.Shift) {
                        var d = _renderer as DarkKn5ObjectRenderer;
                        if (d != null) {
                            d.SslrAdjustCurrentMode = d.SslrAdjustCurrentMode.PreviousValue();
                        }
                    }
                    break;

                case Keys.D2:
                    if (!args.Control && !args.Alt && !args.Shift) {
                        var d = _renderer as DarkKn5ObjectRenderer;
                        if (d != null) {
                            d.SslrAdjustCurrentMode = d.SslrAdjustCurrentMode.NextValue();
                        }
                    }
                    break;
#endif

                case Keys.R:
                    if (!args.Control && !args.Alt && !args.Shift) {
                        var d = _renderer as DarkKn5ObjectRenderer;
                        if (d != null) {
                            d.UseSslr = !d.UseSslr;
                        }
                    }
                    break;

                case Keys.T:
                    if (!args.Control && !args.Alt && !args.Shift) {
                        _renderer.ToneMapping = _renderer.ToneMapping.NextValue();
                    }
                    break;

                case Keys.A: {
                    var d = _renderer as DarkKn5ObjectRenderer;
                    if (d != null) {
                        if (!args.Control && !args.Alt && !args.Shift) {
                            d.UseAo = !d.UseAo;
                        }
                        if (!args.Control && !args.Alt && args.Shift) {
                            d.AoDebug = !d.AoDebug;
                        }
                        if (args.Control && !args.Alt && args.Shift) {
                            d.AoType = d.AoType.NextValue();
                        }
                    }
                    break;
                }

                case Keys.Space:
                    if (!args.Control && args.Alt && !args.Shift && !Kn5ObjectRenderer.LockCamera) {
                        Kn5ObjectRenderer.AutoRotate = !Kn5ObjectRenderer.AutoRotate;
                    }
                    break;

                case Keys.End:
                    if (!Kn5ObjectRenderer.LockCamera) {
                        _renderer.AutoAdjustTarget = !_renderer.AutoAdjustTarget;
                    }
                    break;

                case Keys.F1:
                    if (!args.Control && !args.Alt && !args.Shift && !Kn5ObjectRenderer.LockCamera) {
                        _renderer.NextCamera();
                    }
                    break;

                case Keys.F6:
                    if (!args.Control && !args.Alt && !args.Shift && !Kn5ObjectRenderer.LockCamera) {
                        _renderer.NextExtraCamera();
                    }
                    break;

                case Keys.F7:
                    if (!Kn5ObjectRenderer.LockCamera) {
                        if (args.Alt) {
                            _renderer.UseInterpolationCamera = !_renderer.UseInterpolationCamera;
                        } else {
                            _renderer.UseFpsCamera = !_renderer.UseFpsCamera;
                        }
                    }
                    break;

                case Keys.F8:
                    Shot();
                    break;

                case Keys.F:
                    if (args.Control && !args.Alt && !args.Shift) {
                        _renderer.UseSmaa = !_renderer.UseSmaa;
                    } else if (!args.Control && args.Alt && !args.Shift) {
                        if (_renderer.CarNode != null) {
                            _renderer.CarNode.FansEnabled = !_renderer.CarNode.FansEnabled;
                        }
                    } else {
                        _renderer.UseFxaa = !_renderer.UseFxaa;
                    }
                    break;

                case Keys.M:
                    if (args.Control && !args.Alt && !args.Shift) {
                        _renderer.UseSsaa = !_renderer.UseSsaa;
                    } else if (args.Alt && !args.Shift) {
                        var d = _renderer as DarkKn5ObjectRenderer;
                        if (d != null) {
                            if (args.Control) {
                                d.FlatMirrorBlurred = !d.FlatMirrorBlurred;
                            } else {
                                d.FlatMirror = !d.FlatMirror;
                            }
                        }
                    } else {
                        _renderer.UseMsaa = !_renderer.UseMsaa;
                    }
                    break;

                case Keys.W:
                    if (!args.Control && args.Alt && !args.Shift) {
                        if (_renderer.CarNode != null) {
                            _renderer.CarNode.WipersEnabled = !_renderer.CarNode.WipersEnabled;
                        }
                    } else {
                        _renderer.ShowWireframe = !_renderer.ShowWireframe;
                    }
                    break;

                case Keys.E:
                    if (!args.Control &&! args.Alt && !args.Shift) {
                        var d = _renderer as DarkKn5ObjectRenderer;
                        if (d != null) {
                            d.CubemapAmbient = d.CubemapAmbient != 0f ? 0f : 0.5f;
                        }
                    } else if (args.Control &&! args.Alt && !args.Shift) {
                        var d = _renderer as DarkKn5ObjectRenderer;
                        if (d != null) {
                            d.ReflectionsWithShadows = !d.ReflectionsWithShadows;
                        }
                    }
                    break;

                case Keys.B:
                    if (!args.Control && args.Alt && !args.Shift) {
                        if (_renderer.CarNode != null) {
                            _renderer.CarNode.BlurredNodesActive = !_renderer.CarNode.BlurredNodesActive;
                        }
                    } else if (!args.Control && !args.Alt && args.Shift) {
                        _renderer.ToneMapping = _renderer.ToneMapping == ToneMappingFn.None ? ToneMappingFn.Reinhard : ToneMappingFn.Filmic;
                    } else if (args.Control && !args.Alt && !args.Shift) {
                        _renderer.UseColorGrading = !_renderer.UseColorGrading;
                    } else {
                        _renderer.UseBloom = !_renderer.UseBloom;
                    }
                    break;

                case Keys.D:
                    if (_renderer.CarNode != null) {
                        if (!args.Shift) {
                            if (!args.Control && !args.Alt) {
                                var all = _renderer.CarNode.RightDoorOpen && _renderer.CarNode.LeftDoorOpen;
                                _renderer.CarNode.RightDoorOpen = !all;
                                _renderer.CarNode.LeftDoorOpen = !all;
                            } else if (!args.Control) {
                                _renderer.CarNode.RightDoorOpen = !_renderer.CarNode.RightDoorOpen;
                            } else {
                                _renderer.CarNode.LeftDoorOpen = !_renderer.CarNode.LeftDoorOpen;
                            }
                        } else if (args.Control && !args.Alt) {
                            _renderer.CarNode.IsDriverVisible = !_renderer.CarNode.IsDriverVisible;
                        }
                    }
                    break;

                case Keys.C:
                    if (_renderer.CarNode != null) {
                        if (!args.Control && args.Alt && !args.Shift) {
                            _renderer.CarNode.CockpitLrActive = !_renderer.CarNode.CockpitLrActive;
                        } else if (args.Control && !args.Alt && args.Shift) {
                            _renderer.CarNode.IsCrewVisible = !_renderer.CarNode.IsCrewVisible;
                        } else {
                            _renderer.CarNode.IsColliderVisible = !_renderer.CarNode.IsColliderVisible;
                        }
                    }
                    break;

                case Keys.NumPad7:
                    if (!args.Alt) {
                        _renderer.CarNode?.ToggleExtra(0);
                    }
                    break;

                case Keys.NumPad8:
                    if (!args.Alt) {
                        _renderer.CarNode?.ToggleExtra(1);
                    }
                    break;

                case Keys.NumPad9:
                    if (!args.Alt) {
                        _renderer.CarNode?.ToggleExtra(2);
                    }
                    break;

                case Keys.NumPad4:
                    if (!args.Alt) {
                        _renderer.AnimationsMultipler = 0.2f;
                    }
                    break;

                case Keys.NumPad5:
                    if (!args.Alt) {
                        _renderer.AnimationsMultipler = 1f;
                    }
                    break;

                case Keys.NumPad6:
                    if (!args.Alt) {
                        _renderer.AnimationsMultipler = 2.5f;
                    }
                    break;

                case Keys.NumPad0:
                    if (!args.Alt) {
                        _renderer.CarNode?.ToggleWing(0);
                    }
                    break;

                case Keys.NumPad1:
                    if (!args.Alt) {
                        _renderer.CarNode?.ToggleWing(1);
                    }
                    break;

                case Keys.NumPad2:
                    if (!args.Alt) {
                        _renderer.CarNode?.ToggleWing(2);
                    }
                    break;

                case Keys.NumPad3:
                    if (!args.Alt) {
                        _renderer.CarNode?.ToggleWing(3);
                    }
                    break;

                case Keys.S:
                    if (args.Control && !args.Alt && !args.Shift) {
                        _renderer.EnableShadows = !_renderer.EnableShadows;
                    }
                    if (args.Control && !args.Alt && args.Shift) {
                        _renderer.UsePcss = !_renderer.UsePcss;
                    }
                    if (args.Control && args.Alt && !args.Shift) {
                        var d = _renderer as DarkKn5ObjectRenderer;
                        if (d != null) {
                            var sizes = new[] { 1024, 2048, 4096, 8192 };
                            d.ShadowMapSize = sizes.ElementAtOr(sizes.IndexOf(d.ShadowMapSize) + 1, sizes[0]);
                        }
                    }
                    if (!args.Control && !args.Alt && !args.Shift) {
                        if (ReplaceableShowroom) {
                            var dialog = new OpenFileDialog {
                                Title = @"Select Showroom",
                                Filter = @"KN5 Files (*.kn5)|*.kn5"
                            };

                            if (dialog.ShowDialog() == DialogResult.OK) {
                                _renderer.SetShowroom(dialog.FileName);
                            }
                        }
                    }
                    if (!args.Control && args.Alt && !args.Shift) {
                        if (_renderer.CarNode != null) {
                            _renderer.CarNode.SeatbeltOnActive = !_renderer.CarNode.SeatbeltOnActive;
                        }
                    }
                    break;

                case Keys.PageUp:
                    if (!args.Control && args.Alt && !args.Shift) {
                        _renderer.SelectPreviousLod();
                    }
                    break;

                case Keys.PageDown:
                    if (!args.Control && args.Alt && !args.Shift) {
                        _renderer.SelectNextLod();
                    }
                    break;

                case Keys.U:
                    if (args.Control && !args.Alt && !args.Shift) {
                        var d = _renderer as DarkKn5ObjectRenderer;
                        if (d != null) {
                            d.MeshDebug = !d.MeshDebug;
                        }
                    } else if (!args.Control && !args.Alt && !args.Shift) {
                        var d = _renderer as DarkKn5ObjectRenderer;
                        if (d != null) {
                            d.SuspensionDebug = !d.SuspensionDebug;
                        }
                    } else if (args.Shift && args.Control && !args.Alt) {
                        if (_renderer.CarNode != null) {
                            _renderer.CarNode.UseUp = !_renderer.CarNode.UseUp;
                        }
                    }
                    break;
            }
        }
    }
}