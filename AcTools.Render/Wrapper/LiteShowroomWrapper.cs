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

        protected override void OnTick(object sender, TickEventArgs args) {
            base.OnTick(sender, args);
            
            if (User32.IsKeyPressed(Keys.LMenu) || User32.IsKeyPressed(Keys.RMenu)) {
                if (_renderer.CarNode == null) return;

                if (User32.IsKeyPressed(Keys.Left)) {
                    _renderer.CarNode.SteerDeg += (30f - _renderer.CarNode.SteerDeg) / 20f;
                }

                if (User32.IsKeyPressed(Keys.Right)) {
                    _renderer.CarNode.SteerDeg += (-30f - _renderer.CarNode.SteerDeg) / 20f;
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
                case Keys.Space:
                    if (!args.Control && args.Alt && !args.Shift) {
                        Kn5ObjectRenderer.AutoRotate = !Kn5ObjectRenderer.AutoRotate;
                    }
                    break;

                case Keys.End:
                    _renderer.AutoAdjustTarget = true;
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
                        using (var image = _renderer.Shot(multipler, 1d)) {
                            var directory = FileUtils.GetDocumentsScreensDirectory();
                            FileUtils.EnsureDirectoryExists(directory);
                            var filename = Path.Combine(directory, $"__custom_showroom_{DateTime.Now.ToUnixTimestamp()}.jpg");
                            if (downscale) {
                                using (var down = image.HighQualityResize(new Size(image.Width / 2, image.Height / 2))) {
                                    down.Save(filename);
                                }
                            } else {
                                using (var down = image.CopyImage()) {
                                    down.Save(filename);
                                }
                            }
                        }
                    }
                    break;

                case Keys.F:
                    if (args.Control && !args.Alt && !args.Shift) {
                        _renderer.UseSmaa = !_renderer.UseSmaa;
                    } else {
                        _renderer.UseFxaa = !_renderer.UseFxaa;
                    }
                    break;

                case Keys.M:
                    if (args.Control && !args.Alt && !args.Shift) {
                        _renderer.UseSsaa = !_renderer.UseSsaa;
                    } else if (!args.Control && args.Alt && !args.Shift) {
                        var d = _renderer as DarkKn5ObjectRenderer;
                        if (d != null) {
                            d.FlatMirror = !d.FlatMirror;
                        }
                    } else {
                        _renderer.UseMsaa = !_renderer.UseMsaa;
                    }
                    break;

                case Keys.W:
                    _renderer.ShowWireframe = !_renderer.ShowWireframe;
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

                case Keys.C:
                    if (!args.Control && args.Alt && !args.Shift) {
                        if (_renderer.CarNode != null) {
                            _renderer.CarNode.CockpitLrActive = !_renderer.CarNode.CockpitLrActive;
                        }
                    }
                    break;

                case Keys.S:
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
                    }
                    break;
            }
        }
    }
}