using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using AcTools.DataFile;
using AcTools.Utils;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DirectInput;
using SlimDX.DXGI;
using SlimDX.Windows;
using Resource = SlimDX.Direct3D11.Resource;

namespace AcTools.Kn5Render.Kn5Render {
    public partial class Render : System.IDisposable {
        RenderForm _form;
        SwapChain _form_swapChain;
        Point _form_lastMousePos;

        public void Form(int width = 512, int height = 512, bool editMode = false) {
            Form_DxCreate(width, height);
            Form_DxResize();
            Form_InitEvents();

            _camera.Save();

            MessagePump.Run(_form, () => {
                if (_form.Focused) {
                    if (IsKeyDown(Keys.Up)) _camera.Walk(0.1f);
                    if (IsKeyDown(Keys.Down)) _camera.Walk(-0.1f);
                    if (IsKeyDown(Keys.Left)) _camera.Strafe(-0.1f);
                    if (IsKeyDown(Keys.Right)) _camera.Strafe(0.1f);
                }

                DrawFrame();
                DrawPreviousFrameTo(_form_renderTarget);
                _form_swapChain.Present(1, PresentFlags.None);
            });

            if (editMode) {
                Form_ToogleEditMode();
            }
        }

        private Timer _toastTimer;
        private string _defaultText;

        public void Toast(string message) {
            if (_form == null) return;

            Action action = delegate {
                if (_toastTimer == null) {
                    _toastTimer = new Timer { Interval = 3000 };
                    _toastTimer.Tick += ToastTimer_Tick;
                }

                _form.Text = _form.Text == _defaultText ? message : _form.Text + ", " + message;
                _toastTimer.Enabled = false;
                _toastTimer.Enabled = true;
            };

            _form.Invoke(action);
        }

        void ToastTimer_Tick(object sender, EventArgs e) {
            if (_form == null) return;
            _form.Text = _defaultText;
            _toastTimer.Enabled = false;
        }

        void Form_DxCreate(int width, int height) {
            try {
                _defaultText = new IniFile(Path.GetDirectoryName(_filename), "car.ini")["INFO"].Get("SCREEN_NAME");
            } catch (Exception) {
                _defaultText = Path.GetFileNameWithoutExtension(_filename);
            }

            _form = new RenderForm(_defaultText) {
                Width = width,
                Height = height
            };

            _width = _form.ClientSize.Width;
            _height = _form.ClientSize.Height;

            var factory = new Factory();
            _form_swapChain = new SwapChain(factory, CurrentDevice, new SwapChainDescription() {
                BufferCount = 2,
                Usage = Usage.RenderTargetOutput,
                OutputHandle = _form.Handle,
                IsWindowed = true,
                ModeDescription = new ModeDescription(0, 0, new Rational(60, 1), Format.R8G8B8A8_UNorm),
                SampleDescription = new SampleDescription(1, 0),
                Flags = SwapChainFlags.AllowModeSwitch,
                SwapEffect = SwapEffect.Discard
            });

            factory.SetWindowAssociation(_form.Handle, WindowAssociationFlags.IgnoreAltEnter);
            factory.Dispose();
        }

        private bool _form_editMode = false;
        void Form_ToogleEditMode() {
            if (_form_editMode) {
                Toast("Skin editing mode disabled");
                _form.FormBorderStyle = FormBorderStyle.Sizable;
                _form.Width = 1280;
                _form.Height = 720;
                _form.Left = (Screen.PrimaryScreen.WorkingArea.Width - _form.Width) / 2;
                _form.Top = (Screen.PrimaryScreen.WorkingArea.Height - _form.Height) / 2;
                _form.TopMost = false;
            } else {
                Toast("Skin editing mode enabled");
                _form.TopMost = true;
                _form.FormBorderStyle = FormBorderStyle.None;
                _form.Width = 400;
                _form.Height = 240;
                _form.Top = Screen.PrimaryScreen.WorkingArea.Height - 300;
                _form.Left = 80;
            }

            _width = _form.ClientSize.Width;
            _height = _form.ClientSize.Height;
            Form_DxResize();

            _form_editMode = !_form_editMode;
        }

        private bool _form_brightMode = false;

        private void Form_ToogleBrightMode() {
            if (_form_brightMode) {
                Toast("Bright mode disabled");

                _dirLight = new DirectionalLight {
                    Ambient = new Color4(1.45f, 1.46f, 1.44f),
                    Diffuse = new Color4(2.24f, 2.23f, 2.20f),
                    Specular = new Color4(0.0f, 0.0f, 0.0f),
                    Direction = new Vector3(-1.57735f, -2.57735f, 0.57735f)
                };
                _backgroundColor = new Color4(0.0f, 0.0f, 0.0f);
                _effectTest.FxDirLight.Set(_dirLight);

                foreach (var x in _objs.Where(x => !(x is MeshObjectKn5) && !(x is MeshObjectShadow))) {
                    x.Visible = true;
                }
            } else {
                Toast("Bright mode enabled");

                _dirLight = new DirectionalLight {
                    Ambient = new Color4(3.85f, 3.86f, 3.84f),
                    Diffuse = new Color4(2.24f, 2.27f, 2.28f),
                    Specular = new Color4(0.0f, 0.0f, 0.0f),
                    Direction = new Vector3(1.57735f, -2.57735f, 0.57735f)
                };
                _backgroundColor = new Color4(0.9f, 0.92f, 0.97f);
                _effectTest.FxDirLight.Set(_dirLight);

                foreach (var x in _objs.Where(x => !(x is MeshObjectKn5) && !(x is MeshObjectShadow))) {
                    x.Visible = false;
                }
            }

            _form_brightMode = !_form_brightMode;
        }

        [DllImport("user32.dll")]
        static extern short GetAsyncKeyState(System.Windows.Forms.Keys vKey);

        public static bool IsKeyDown(System.Windows.Forms.Keys key) {
            return (GetAsyncKeyState(key) & 0x8000) != 0;
        }

        bool _form_moveMode;

        void Form_InitEvents() {
            _form.KeyDown += (o, e) => {
                if (e.Alt && e.KeyCode == Keys.Enter) _form_swapChain.IsFullScreen = !_form_swapChain.IsFullScreen;
                if (e.KeyCode == Keys.W) _wireframeMode = !_wireframeMode;
                if (e.KeyCode == Keys.PageUp) LoadSkin(SelectedSkin - 1);
                if (e.KeyCode == Keys.PageDown) LoadSkin(SelectedSkin + 1);
                if (e.KeyCode == Keys.Home) {
                    _camera.Restore();
                }

                if (e.KeyCode == Keys.F2 || e.KeyCode == Keys.Escape && _form_editMode) {
                    Form_ToogleEditMode();
                } else if (e.KeyCode == Keys.Escape && !_form_editMode) {
                    _form.Close();
                }

                if (e.KeyCode == Keys.F3) {
                    Form_ToogleBrightMode();
                }
            };

            _form.MouseWheel += (o, e) => {
                _camera.Zoom(e.Delta > 0 ? -0.4f : 0.4f);
            };

            _form.MouseDown += (o, e) => {
                _form_moveMode = e.Y < 20 && _form_editMode;
            };

            _form.MouseMove += (o, e) => {
                if (e.Button == MouseButtons.Middle || e.Button == MouseButtons.Left && IsKeyDown(Keys.Space)) {
                    var size = 180.0f / Math.Min(_form.Height, _form.Width);

                    var dx = MathF.ToRadians(size * (e.X - _form_lastMousePos.X));
                    var dy = MathF.ToRadians(size * (e.Y - _form_lastMousePos.Y));

                    var c = _camera as CameraOrbit;
                    if (c != null) {
                        c.Target += dy * _camera.Up - dx * _camera.Right;
                    }
                } else if (e.Button == MouseButtons.Left) {
                    if (_form_moveMode) {
                        _form.Left += e.X - _form_lastMousePos.X;
                        _form.Top += e.Y - _form_lastMousePos.Y;

                        _form_lastMousePos = new Point(e.X - e.X + _form_lastMousePos.X,
                                                       e.Y - e.Y + _form_lastMousePos.Y);
                        return;
                    }

                    var size = 180.0f / Math.Min(_form.Height, _form.Width);

                    var dx = MathF.ToRadians(size * (e.X - _form_lastMousePos.X));
                    var dy = MathF.ToRadians(size * (e.Y - _form_lastMousePos.Y));

                    _camera.Pitch(dy);
                    _camera.Yaw(-dx);
                } else if (e.Button == MouseButtons.Right) {
                    var size = 180.0f / Math.Min(_form.Height, _form.Width);

                    var dy = MathF.ToRadians(size * (e.Y - _form_lastMousePos.Y));
                    _camera.Zoom(dy * 3.0f);
                }

                _form_lastMousePos = e.Location;
            };

            _form.UserResized += (o, e) => {
                _width = _form.ClientSize.Width;
                _height = _form.ClientSize.Height;
                Form_DxResize();
            };

            _form.MouseClick += (o, e) => {
                if (e.Button == MouseButtons.Middle) {
                    // Form_Pick(e.X, e.Y);
                }

                if (e.Button == MouseButtons.XButton1) {
                    LoadSkin(SelectedSkin - 1);
                }

                if (e.Button == MouseButtons.XButton2) {
                    LoadSkin(SelectedSkin + 1);
                }
            };

            _form.Closed += (sender, args) => {
                Dispose();
            };
        }

        private void Form_Pick(int sx, int sy) {
            var ray = _camera.GetPickingRay(new Vector2(sx, sy), new Vector2(_form.ClientSize.Width, _form.ClientSize.Height));

            foreach (var obj in _objs.OfType<MeshObjectKn5>().Where(obj => obj.Visible)) {
                /*var temp = new Ray(Vector3.TransformCoordinate(ray.Position, obj.InvTransform),
                        Vector3.TransformNormal(ray.Direction, obj.InvTransform));
                temp.Direction.Normalize();*/

                float tmin;
                if (!Ray.Intersects(ray, obj.MeshBox, out tmin)) return;

                obj.Visible = false;
            }
        }

        RenderTargetView _form_renderTarget;

        void Form_DxResize() {
            if (_form_renderTarget != null) {
                _form_renderTarget.Dispose();
            }

            _form_swapChain.ResizeBuffers(1, _width, _height, Format.R8G8B8A8_UNorm, SwapChainFlags.None);
            using (var resource = Resource.FromSwapChain<Texture2D>(_form_swapChain, 0))
                _form_renderTarget = new RenderTargetView(CurrentDevice, resource);

            DxResize();
        }
    }
}
