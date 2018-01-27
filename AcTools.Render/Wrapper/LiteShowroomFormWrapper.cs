// #define SSLR_PARAMETRIZED

using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows.Forms;
using AcTools.Render.Base;
using AcTools.Render.Forward;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Render.Kn5SpecificForwardDark;
using AcTools.Render.Kn5SpecificForwardDark.Materials;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.Windows;
using SlimDX;

namespace AcTools.Render.Wrapper {
    public class LiteShowroomFormWrapper : BaseKn5FormWrapper {
        public static long OptionMontageMemoryLimit = 2147483648L;

        private readonly ForwardKn5ObjectRenderer _renderer;

        public bool ReplaceableShowroom { get; set; }

        private bool _editMode;

        public bool EditMode {
            get => _editMode;
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

        public LiteShowroomFormWrapper(ForwardKn5ObjectRenderer renderer, string title = "Lite Showroom", int width = 1600, int height = 900)
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

        protected override void OnLostFocus(object sender, EventArgs e) {
            base.OnLostFocus(sender, e);
            _renderer.StopMovement();
        }

        protected override bool SleepMode => base.SleepMode && Paused;

        protected override void OnMouseMove(object sender, MouseEventArgs e) {
            if (!Form.Focused) {
                _renderer.StopMovement();
            }

            base.OnMouseMove(sender, e);
        }

        protected override void OnMouseMove(MouseButtons button, int dx, int dy) {
            if (_renderer.ShowMovementArrows) {
                _renderer.MousePosition = new Vector2(MousePosition.X, MousePosition.Y);
                _renderer.IsDirty = true;
            }

            var slow = User32.IsKeyPressed(Keys.LMenu) || User32.IsKeyPressed(Keys.RMenu);
            var tryToClone = User32.IsKeyPressed(Keys.LShiftKey) || User32.IsKeyPressed(Keys.RShiftKey);

            if (button != MouseButtons.Left || !_renderer.MoveObject(new Vector2(dx, dy) * (slow ? 0.2f : 1f), tryToClone)) {
                base.OnMouseMove(button, dx, dy);
            }

            if (button != MouseButtons.Left) {
                _renderer.StopMovement();
            }
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

        protected override void OnClick() {
            base.OnClick();
            if (User32.IsKeyPressed(Keys.LControlKey) || User32.IsKeyPressed(Keys.RControlKey)) {
                (Renderer as DarkKn5ObjectRenderer)?.AutoFocus(new Vector2(MousePosition.X, MousePosition.Y));
            }
        }

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

                if (_renderer is DarkKn5ObjectRenderer renderer) {
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

        protected virtual void SplitShotPieces(Size size, bool downscale, string filename, RendererShotFormat format, IProgress<Tuple<string, double?>> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            if (format.IsHdr()) {
                throw new NotSupportedException("Can’t make an HDR-screenshot in super-resolution");
            }

            var dark = (DarkKn5ObjectRenderer)Renderer;
            var destination = filename.ApartFromLast(Path.GetExtension(filename), StringComparison.OrdinalIgnoreCase);
            var information = dark.SplitShot(size.Width, size.Height, downscale ? 0.5d : 1d, destination, progress, cancellation);
            File.WriteAllText(Path.Combine(destination, "join.bat"), $@"@echo off
rem Use magick.exe from ImageMagick for Windows to run this script
rem and combine images: https://www.imagemagick.org/script/binary-releases.php
set MAGICK_TMPDIR=tmp
mkdir tmp
magick.exe montage piece-*-*.{information.Extension} -limit memory {OptionMontageMemoryLimit.ToInvariantString()} -limit map {OptionMontageMemoryLimit.ToInvariantString()} -tile {information.Cuts.ToInvariantString()}x{information.Cuts.ToInvariantString()} -geometry +0+0 out{format.GetExtension()}
rmdir /q tmp
echo @del *-*.{information.Extension} delete-pieces.bat join.bat > delete-pieces.bat");
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        protected void SplitShotInner(Size size, bool downscale, string filename, RendererShotFormat format, IProgress<Tuple<string, double?>> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            SplitShotPieces(size, downscale, filename, format, progress, cancellation);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        protected void ShotInner(Size size, bool downscale, string filename, RendererShotFormat format, IProgress<Tuple<string, double?>> progress = null,
                CancellationToken cancellation = default(CancellationToken)) {
            using (var destination = File.Open(filename, FileMode.Create, FileAccess.ReadWrite)) {
                progress?.Report(Tuple.Create("Rendering…", (double?)0.2));

                if (format.IsWindowsEncoderBroken()) {
                    using (var stream = new MemoryStream()) {
                        _renderer.Shot(size.Width, size.Height, downscale ? 0.5 : 1d, 1d, stream,
                                RendererShotFormat.Png, progress.ToDouble("Rendering…").SubrangeDouble(0.2, 0.6), cancellation);
                        stream.Position = 0;
                        if (cancellation.IsCancellationRequested) return;

                        progress?.Report(Tuple.Create("Saving…", (double?)0.9));
                        ImageUtils.Convert(stream, destination, null, 95, new ImageUtils.ImageInformation());
                    }
                } else {
                    _renderer.Shot(size.Width, size.Height, downscale ? 0.5 : 1d, 1d, destination,
                            format.IsWindowsEncoderBroken() ? RendererShotFormat.Png : format,
                            progress.ToDouble("Rendering…").SubrangeDouble(0.2, 0.9), cancellation);
                }
            }
        }

        protected virtual void SplitShot(Size size, bool downscale, string filename, RendererShotFormat format) {
            SplitShotInner(size, downscale, filename, format);
        }

        protected virtual void Shot(Size size, bool downscale, string filename, RendererShotFormat format) {
            ShotInner(size, downscale, filename, format);
        }

        private void Shot() {
            var splitMode = ImageUtils.IsMagickSupported && Renderer is DarkKn5ObjectRenderer;

            // hold shift to disable downsampling
            // hold ctrl to render scene in 6x resolution
            // hold alt to render scene in 4x resolution
            // hold both for 1x only

            var ctrlPressed = User32.IsKeyPressed(Keys.LControlKey) || User32.IsKeyPressed(Keys.RControlKey);
            var altPressed = User32.IsKeyPressed(Keys.LMenu) || User32.IsKeyPressed(Keys.RMenu);
            var shiftPressed = User32.IsKeyPressed(Keys.LShiftKey) || User32.IsKeyPressed(Keys.RShiftKey);
            var winPressed = User32.IsKeyPressed(Keys.LWin) || User32.IsKeyPressed(Keys.RWin);

            var downscale = !shiftPressed;
            int multiplier;
            if (winPressed && splitMode) {
                multiplier = altPressed ? ctrlPressed ? 48 : 32 : ctrlPressed ? 24 : 16;
            } else if (ctrlPressed) {
                multiplier = altPressed ? 1 : splitMode ? 8 : 4;
            } else if (altPressed) {
                multiplier = splitMode ? 4 : 3;
            } else {
                multiplier = 2;
            }

            var directory = AcPaths.GetDocumentsScreensDirectory();
            FileUtils.EnsureDirectoryExists(directory);
            var filename = Path.Combine(directory, $"__custom_showroom_{DateTime.Now.ToUnixTimestamp()}.jpg");

            if (splitMode && multiplier > 2d) {
                var size = new Size(_renderer.ActualWidth.Round(4) * multiplier, _renderer.ActualHeight.Round(4) * multiplier);
                SplitShot(size, downscale, filename, RendererShotFormat.Jpeg);
            } else {
                var size = new Size(_renderer.ActualWidth * multiplier, _renderer.ActualHeight * multiplier);
                Shot(size, downscale, filename, RendererShotFormat.Jpeg);
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

            var dark = _renderer as DarkKn5ObjectRenderer;
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
                        if (dark != null) {
                            dark.UseSslr = !dark.UseSslr;
                        }
                    }
                    if (!args.Control && args.Alt && !args.Shift) {
                        if (dark != null) {
                            dark.ReflectionCubemapAtCamera = !dark.ReflectionCubemapAtCamera;
                        }
                    }
                    break;

                case Keys.T:
                    if (args.Control && args.Alt && !args.Shift) {
                        _renderer.ToneMapping = _renderer.ToneMapping.NextValue();
                    } else if (!args.Control && !args.Alt && !args.Shift) {
                        _renderer.ShowMovementArrows = !_renderer.ShowMovementArrows;
                    } else if (dark != null) {
                        if (args.Control && !args.Alt && !args.Shift) {
                            dark.TesselationMode = TesselationMode.Phong;
                            Renderer.IsDirty = true;
                        } else if (!args.Control && args.Alt && !args.Shift) {
                            dark.TesselationMode = TesselationMode.Pn;
                            Renderer.IsDirty = true;
                        } else if (!args.Control && !args.Alt && args.Shift) {
                            dark.TesselationMode = TesselationMode.Disabled;
                            Renderer.IsDirty = true;
                        }
                    }
                    break;

                case Keys.A:
                    if (dark != null) {
                        if (!args.Control && !args.Alt && !args.Shift) {
                            dark.UseAo = !dark.UseAo;
                        }
                        if (!args.Control && !args.Alt && args.Shift) {
                            dark.AoDebug = !dark.AoDebug;
                        }
                        if (args.Control && !args.Alt && args.Shift) {
                            dark.AoType = EnumExtension.GetValues<AoType>().Where(AoTypeExtension.IsProductionReady)
                                                               .SkipWhile(x => !Equals(x, dark.AoType)).Skip(1).FirstOrDefault();
                        }
                        if (args.Control && args.Alt && !args.Shift) {
                            dark.AddMovingLight();
                        }
                        if (args.Control && args.Alt && args.Shift) {
                            dark.AddLight();
                        }
                    }
                    break;

                case Keys.Z:
                    if (dark != null) {
                        if (args.Control && args.Alt && !args.Shift) {
                            dark.RemoveMovingLight();
                        }
                        if (args.Control && args.Alt && args.Shift) {
                            dark.RemoveLight();
                        }
                    }
                    break;

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
                    } else if (args.Control && args.Alt && !args.Shift) {
                        if (_renderer.CarNode != null) {
                            _renderer.CarNode.AreFlamesVisible = !_renderer.CarNode.AreFlamesVisible;
                        }
                    } else if (args.Control && args.Alt && args.Shift) {
                        if (_renderer.CarNode != null) {
                            _renderer.CarNode.IsFuelTankVisible = !_renderer.CarNode.IsFuelTankVisible;
                        }
                    } else {
                        _renderer.UseFxaa = !_renderer.UseFxaa;
                    }
                    break;

                case Keys.M:
                    if (args.Control && !args.Alt && !args.Shift) {
                        _renderer.UseSsaa = !_renderer.UseSsaa;
                    } else if (args.Alt && !args.Shift) {
                        if (dark != null) {
                            if (args.Control) {
                                dark.FlatMirrorBlurred = !dark.FlatMirrorBlurred;
                            } else {
                                dark.FlatMirror = !dark.FlatMirror;
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
                    } else if (dark != null) {
                        dark.WireframeMode = dark.WireframeMode.NextValue();
                    }
                    break;

                case Keys.J:
                    if (dark != null) {
                        if (!args.Control && !args.Alt && !args.Shift) {
                            dark.UseCorrectAmbientShadows = !dark.UseCorrectAmbientShadows;
                        } else if (!args.Control && args.Alt && !args.Shift) {
                            dark.BlurCorrectAmbientShadows = !dark.BlurCorrectAmbientShadows;
                        }
                    }
                    break;

                case Keys.E:
                    if (!args.Control &&! args.Alt && !args.Shift) {
                        if (dark != null) {
                            dark.CubemapAmbient = dark.CubemapAmbient != 0f ? 0f : 0.5f;
                        }
                    } else if (args.Control &&! args.Alt && !args.Shift) {
                        if (dark != null) {
                            dark.ReflectionsWithShadows = !dark.ReflectionsWithShadows;
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

                case Keys.P:
                    if (!args.Control && args.Alt && !args.Shift) {
                        if (dark != null) {
                            dark.ShowDepth = !dark.ShowDepth;
                        }
                    }
                    break;

                case Keys.Y:
                    if (_renderer.CarNode != null) {
                        if (!args.Control && !args.Alt && !args.Shift) {
                            _renderer.CarNode.SoundEngineActive = !_renderer.CarNode.SoundEngineActive;
                        } else if (args.Control && !args.Alt && !args.Shift) {
                            _renderer.CarNode.SoundEngineExternal = !_renderer.CarNode.SoundEngineExternal;
                        } else if (!args.Control && args.Alt && !args.Shift) {
                            _renderer.CarNode.SoundHorn = !_renderer.CarNode.SoundHorn;
                        }
                    }
                    break;

                case Keys.O:
                    if (dark != null) {
                        if (!args.Control && !args.Alt && !args.Shift) {
                            dark.UseDof = !dark.UseDof;
                        }
                        if (!args.Control && args.Alt && !args.Shift) {
                            dark.UseAccumulationDof = !dark.UseAccumulationDof;
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
                        _renderer.TimeFactor = 0.2f;
                    }
                    break;

                case Keys.NumPad5:
                    if (!args.Alt) {
                        _renderer.TimeFactor = 1f;
                    }
                    break;

                case Keys.NumPad6:
                    if (!args.Alt) {
                        _renderer.TimeFactor = 2.5f;
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
                        if (dark != null) {
                            var sizes = new[] { 1024, 2048, 4096, 8192 };
                            dark.ShadowMapSize = sizes.ElementAtOr(sizes.IndexOf(dark.ShadowMapSize) + 1, sizes[0]);
                        }
                    }
                    if (!args.Control && !args.Alt && !args.Shift) {
                        if (ReplaceableShowroom) {
                            var dialog = new OpenFileDialog {
                                Title = @"Select showroom",
                                Filter = @"KN5 files (*.kn5)|*.kn5"
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
                        _renderer.MainSlot.SelectPreviousLod();
                    }
                    break;

                case Keys.PageDown:
                    if (!args.Control && args.Alt && !args.Shift) {
                        _renderer.MainSlot.SelectNextLod();
                    }
                    break;

                case Keys.U:
                    if (args.Control && !args.Alt && !args.Shift) {
                        if (dark != null) {
                            dark.MeshDebug = !dark.MeshDebug;
                        }
                    } else if (!args.Control && !args.Alt && !args.Shift) {
                        if (_renderer.CarNode != null) {
                            _renderer.CarNode.SuspensionDebug = !_renderer.CarNode.SuspensionDebug;
                        }
                    } else if (args.Shift && args.Control && !args.Alt) {
                        if (_renderer.CarNode != null) {
                            _renderer.CarNode.UseUp = !_renderer.CarNode.UseUp;
                        }
                    }
                    break;

                case Keys.Q:
                    if (args.Control && !args.Alt && !args.Shift) {
                        if (_renderer.CarNode != null) {
                            _renderer.CarNode.AreWingsVisible = !_renderer.CarNode.AreWingsVisible;
                        }
                    }
                    if (!args.Control && args.Alt && !args.Shift) {
                        if (_renderer.CarNode != null) {
                            _renderer.CarNode.AreWheelsContoursVisible = !_renderer.CarNode.AreWheelsContoursVisible;
                        }
                    }
                    if (!args.Control && !args.Alt && !args.Shift) {
                        if (_renderer.CarNode != null) {
                            _renderer.CarNode.AlignWheelsByData = !_renderer.CarNode.AlignWheelsByData;
                        }
                    }
                    break;
            }
        }
    }
}