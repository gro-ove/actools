using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using AcTools.Render.Base;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Render.Kn5SpecificForwardDark;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.Windows;
using SlimDX;

namespace AcTools.Render.Wrapper {
    public class LiteShowroomWrapper : BaseKn5FormWrapper {
        private readonly ForwardKn5ObjectRenderer _renderer;

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
            UpdateSize();
        }

        private void UpdateSize() {
            Renderer.Width = Form.ClientSize.Width;
            Renderer.Height = Form.ClientSize.Height;
        }

        protected override void OnResize(object sender, EventArgs eventArgs) {
            UpdateSize();
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

            Renderer.Width = Form.ClientSize.Width;
            Renderer.Height = Form.ClientSize.Height;
        }

        protected virtual void GoToToolMode() {
            Form.FormBorderStyle = FormBorderStyle.SizableToolWindow;
            Form.Width = 400;
            Form.Height = 240;
            Form.Top = Screen.PrimaryScreen.WorkingArea.Height - 300;
            Form.Left = 80;
            Form.TopMost = true;
            Kn5ObjectRenderer.VisibleUi = false;

            Renderer.Width = Form.ClientSize.Width;
            Renderer.Height = Form.ClientSize.Height;
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
            var sslrSetupMode = (_renderer as DarkKn5ObjectRenderer)?.SslrAdjustCurrentMode > DarkKn5ObjectRenderer.SslrAdjustMode.None;

            if (!sslrSetupMode) {
                base.OnTick(sender, args);
            }

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
                        renderer.ReflectionPower += (1f - renderer.ReflectionPower) / 12f;
                    }

                    if (User32.IsKeyPressed(Keys.Down)) {
                        renderer.ReflectionPower += (0f - renderer.ReflectionPower) / 12f;
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
            } else if (sslrSetupMode && (User32.IsKeyPressed(Keys.LControlKey) || User32.IsKeyPressed(Keys.RControlKey))) {
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

                case Keys.R:
                    if (!args.Control && !args.Alt && !args.Shift) {
                        var d = _renderer as DarkKn5ObjectRenderer;
                        if (d != null) {
                            d.UseSslr = !d.UseSslr;
                        }
                    }
                    break;

                case Keys.Space:
                    if (!args.Control && args.Alt && !args.Shift) {
                        Kn5ObjectRenderer.AutoRotate = !Kn5ObjectRenderer.AutoRotate;
                    }
                    break;

                case Keys.End:
                    _renderer.AutoAdjustTarget = !_renderer.AutoAdjustTarget;
                    break;

                case Keys.F1:
                    if (!args.Control && !args.Alt && !args.Shift) {
                        _renderer.NextCamera();
                    }
                    break;

                case Keys.F6:
                    if (!args.Control && !args.Alt && !args.Shift) {
                        _renderer.NextExtraCamera();
                    }
                    break;

                case Keys.F7:
                    if (args.Alt) {
                        _renderer.UseInterpolationCamera = !_renderer.UseInterpolationCamera;
                    } else {
                        _renderer.UseFpsCamera = !_renderer.UseFpsCamera;
                    }
                    break;

                case Keys.F8:
                    {
                        double multipler;
                        bool downscale;

                        {
                            // hold shift to disable downsampling
                            // hold ctrl to render scene in 8x resolution
                            // hold alt to render scene in 4x resolution
                            // hold both for 1x only

                            var ctrlPressed = User32.IsKeyPressed(Keys.LControlKey) || User32.IsKeyPressed(Keys.RControlKey);
                            var altPressed = User32.IsKeyPressed(Keys.LMenu) || User32.IsKeyPressed(Keys.RMenu);
                            var shiftPressed = User32.IsKeyPressed(Keys.LShiftKey) || User32.IsKeyPressed(Keys.RShiftKey);

                            downscale = !shiftPressed;

                            if (ctrlPressed) {
                                multipler = altPressed ? 1d : 8d;
                            } else if (altPressed) {
                                multipler = 4d;
                            } else {
                                multipler = 2d;
                            }
                        }

                        _renderer.KeepFxaaWhileShooting = !downscale;

                        var directory = FileUtils.GetDocumentsScreensDirectory();
                        FileUtils.EnsureDirectoryExists(directory);
                        var filename = Path.Combine(directory, $"__custom_showroom_{DateTime.Now.ToUnixTimestamp()}.jpg");

                        using (var stream = new MemoryStream()) {
                            _renderer.Shot(multipler, 1d, stream, true);
                            stream.Position = 0;

                            using (var destination = File.Open(filename, FileMode.Create, FileAccess.ReadWrite)) {
                                ImageUtils.Convert(stream, destination, downscale
                                        ? new Size((int)(_renderer.ActualWidth * multipler / 2), (int)(_renderer.ActualHeight * multipler / 2))
                                        : (Size?)null);
                            }
                        }
                    }
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
                    } else if (!args.Control && !args.Alt && args.Shift) {
                        _renderer.TemporaryFlag = !_renderer.TemporaryFlag;
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

                case Keys.B:
                    if (!args.Control && args.Alt && !args.Shift) {
                        if (_renderer.CarNode != null) {
                            _renderer.CarNode.BlurredNodesActive = !_renderer.CarNode.BlurredNodesActive;
                        }
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
                        _renderer.EnablePcssShadows = !_renderer.EnablePcssShadows;
                    }
                    if (!args.Control && !args.Alt && !args.Shift) {
                        if (_renderer.CarNode != null) {
                            //_renderer.CarNode.WingsTest = !_renderer.CarNode.WingsTest;
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