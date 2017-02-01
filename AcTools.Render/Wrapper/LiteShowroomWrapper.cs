using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.Windows;

namespace AcTools.Render.Wrapper {
    public class LiteShowroomWrapper : BaseKn5FormWrapper {
        private readonly ForwardKn5ObjectRenderer _renderer;
        private readonly double _resolutionMultiplicator;
        private double _actualResolutionMultiplicator;

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

        public LiteShowroomWrapper(ForwardKn5ObjectRenderer renderer, string title = "Lite Showroom", int width = 1600, int height = 900,
                double resolutionMultiplicator = 1d) : base(renderer, title, width, height) {
            Form.MouseDoubleClick += OnMouseDoubleClick;

            _renderer = renderer;

            if (resolutionMultiplicator < 0) {
                _resolutionMultiplicator = -resolutionMultiplicator;
                _actualResolutionMultiplicator = 1d;
            } else {
                _resolutionMultiplicator = resolutionMultiplicator;
                _actualResolutionMultiplicator = resolutionMultiplicator;
            }

            UpdateSize();
        }

        private void UpdateSize() {
            Renderer.Width = (int)(Form.ClientSize.Width * _actualResolutionMultiplicator);
            Renderer.Height = (int)(Form.ClientSize.Height * _actualResolutionMultiplicator);
            Renderer.OutputDownscaleMultipler = 1 / _actualResolutionMultiplicator;
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
                    if (!_renderer.UseMsaa) {
                        int multipler;
                        bool downscale;

                        {
                            // hold shift to disable downsampling
                            // hold ctrl to render scene in 8x resolution
                            // hold alt to render scene in 4x resolution
                            // hold both for 16x (but any videocard most likely won’t be able to pull this off)

                            var ctrlPressed = User32.IsKeyPressed(Keys.LControlKey) || User32.IsKeyPressed(Keys.RControlKey);
                            var altPressed = User32.IsKeyPressed(Keys.LMenu) || User32.IsKeyPressed(Keys.RMenu);
                            var shiftPressed = User32.IsKeyPressed(Keys.LShiftKey) || User32.IsKeyPressed(Keys.RShiftKey);

                            downscale = !shiftPressed;

                            if (ctrlPressed) {
                                multipler = altPressed ? 16 : 8;
                            } else if (altPressed) {
                                multipler = 4;
                            } else {
                                multipler = 2;
                            }
                        }

                        _renderer.KeepFxaaWhileShooting = !downscale;
                        var image = _renderer.Shot(multipler);
                        var directory = FileUtils.GetDocumentsScreensDirectory();
                        FileUtils.EnsureDirectoryExists(directory);
                        var filename = Path.Combine(directory, $"__custom_showroom_{DateTime.Now.ToUnixTimestamp()}.jpg");
                        if (downscale) {
                            image = image.HighQualityResize(new Size(image.Width / 2, image.Height / 2));
                        }

                        image.Save(filename);
                    }
                    break;

                case Keys.F:
                    _renderer.UseFxaa = !_renderer.UseFxaa;
                    break;

                case Keys.M:
                    _actualResolutionMultiplicator = Equals(_actualResolutionMultiplicator, _resolutionMultiplicator) ? 1d :
                            _resolutionMultiplicator;
                    UpdateSize();
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
            }
        }
    }
}