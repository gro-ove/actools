using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using AcManager.Tools.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Render.Wrapper;
using FirstFloor.ModernUI.Helpers;
using KeyEventArgs = System.Windows.Forms.KeyEventArgs;

namespace AcManager.Controls.CustomShowroom {
    public class LiteShowroomWrapperWithTools : LiteShowroomWrapper {
        private LiteShowroomTools _tools;
        private bool _visibleTools = true;

        public new ForwardKn5ObjectRenderer Kn5ObjectRenderer => (ForwardKn5ObjectRenderer)Renderer;

        public LiteShowroomWrapperWithTools(ForwardKn5ObjectRenderer renderer, CarObject car, string skinId) : base(renderer) {
            GoToNormalMode();

            Form.Closed += OnClosed;
            renderer.VisibleUi = false;

            Form.Move += OnMove;

            _tools = new LiteShowroomTools(renderer, car, skinId) { Owner = null };
            _tools.Show();

            UpdatePosition();

            _tools.Activated += Tools_Activated;
            _tools.Deactivated += Tools_Deactivated;
            _tools.Closed += Tools_Closed;
            _tools.KeyUp += Tools_KeyUp;
            _tools.LocationChanged += Tools_LocationChanged;
            _tools.SizeChanged += Tools_SizeChanged;

            Form.Load += OnLoad;
        }

        private const string KeyNormalSize = "_LiteShowroomWrapperWithTools.NormalSize";
        private const string KeyNormalPos = "_LiteShowroomWrapperWithTools.NormalPos";

        protected sealed override void GoToNormalMode() {
            var area = Screen.PrimaryScreen.WorkingArea;
            var size = ValuesStorage.GetPoint(KeyNormalSize, new Point(1600, 900));
            var pos = ValuesStorage.GetPoint(KeyNormalPos, new Point((area.Width - size.X) / 2, (area.Height - size.Y) / 2));

            Form.Width = MathF.Clamp((int)size.X, 320, area.Width);
            Form.Height = MathF.Clamp((int)size.Y, 200, area.Height);
            Form.Top = MathF.Clamp((int)pos.Y, 0, area.Height - Form.Height);
            Form.Left = MathF.Clamp((int)pos.X, 0, area.Width - Form.Width);

            Form.FormBorderStyle = FormBorderStyle.Sizable;
            Form.TopMost = false;

            Renderer.Width = Form.ClientSize.Width;
            Renderer.Height = Form.ClientSize.Height;

            UpdateVisibility(true);
        }

        private const string KeyToolSize = "_LiteShowroomWrapperWithTools.ToolSize";
        private const string KeyToolPos = "_LiteShowroomWrapperWithTools.ToolPos";

        protected override void GoToToolMode() {
            var area = Screen.PrimaryScreen.WorkingArea;
            var size = ValuesStorage.GetPoint(KeyToolSize, new Point(400, 240));
            var pos = ValuesStorage.GetPoint(KeyToolPos, new Point(80, Screen.PrimaryScreen.WorkingArea.Height - 300));

            Form.Width = MathF.Clamp((int)size.X, 320, area.Width);
            Form.Height = MathF.Clamp((int)size.Y, 200, area.Height);
            Form.Top = MathF.Clamp((int)pos.Y, 0, area.Height - Form.Height);
            Form.Left = MathF.Clamp((int)pos.X, 0, area.Width - Form.Width);

            Form.FormBorderStyle = FormBorderStyle.SizableToolWindow;
            Form.TopMost = true;

            Renderer.Width = Form.ClientSize.Width;
            Renderer.Height = Form.ClientSize.Height;

            UpdateVisibility(true);
        }

        private void Save() {
            if (EditMode) {
                ValuesStorage.Set(KeyToolSize, new Point(Form.Width, Form.Height));
                ValuesStorage.Set(KeyToolPos, new Point(Form.Left, Form.Top));
            } else {
                ValuesStorage.Set(KeyNormalSize, new Point(Form.Width, Form.Height));
                ValuesStorage.Set(KeyNormalPos, new Point(Form.Left, Form.Top));
            }
        }

        private int _stickyLocation;
        private const int StickyLocationsCount = 4;
        private const int Padding = 10;

        private Point? GetStickyLocation(int index, double w, double h) {
            switch (index) {
                case 0:
                    return new Point(Form.Left + Form.Width - w + Padding, Form.Top + Form.Height - h - Padding);

                case 1:
                    return new Point(Form.Left - Padding, Form.Top + Form.Height - h - Padding);

                case 2:
                    return new Point(Form.Left + Form.Width - w + Padding, Form.Top - Padding);

                case 3:
                    return new Point(Form.Left - Padding, Form.Top - Padding);

                default:
                    return null;
            }
        } 

        private bool _skip;

        private void Tools_LocationChanged(object sender, EventArgs e) {
            if (_skip) return;
            
            var pos = new Point(_tools.Left, _tools.Top);
            foreach (var i in from i in Enumerable.Range(0, StickyLocationsCount)
                              let location = GetStickyLocation(i, _tools.Width, _tools.Height)
                              where location.HasValue
                              let delta = pos - location.Value
                              where delta.Length < 20
                              select i) {
                _stickyLocation = i;
                UpdatePosition();
                return;
            }

            _stickyLocation = -1;
        }

        private void Tools_SizeChanged(object sender, SizeChangedEventArgs e) {
            UpdatePosition(e.NewSize.Width, e.NewSize.Height);
        }

        private void UpdatePosition(double w, double h) {
            var location = GetStickyLocation(_stickyLocation, w, h);
            if (location == null || _tools == null) return;

            _skip = true;
            _tools.Left = location.Value.X;
            _tools.Top = location.Value.Y;
            _skip = false;
        }

        private void UpdatePosition() {
            if (_tools == null) return;
            UpdatePosition(_tools.Width, _tools.Height);
        }

        private void OnLoad(object sender, EventArgs e) {
            UpdateVisibility(true);
        }

        protected override void OnKeyUp(object sender, KeyEventArgs args) {
            switch (args.KeyCode) {
                case Keys.H:
                    if (args.Control && !args.Alt && !args.Shift) {
                        _visibleTools = !_visibleTools;
                        UpdateVisibility(true);
                        args.Handled = true;
                    }
                    break;

                case Keys.D:
                    if (args.Control && !args.Alt && !args.Shift) {
                        Kn5ObjectRenderer.Deselect();
                    }
                    break;

                case Keys.Tab:
                    if (!args.Control && !args.Alt && !args.Shift) {
                        args.Handled = true;
                    }
                    break;
            }


            base.OnKeyUp(sender, args);
        }

        private void Tools_KeyUp(object sender, System.Windows.Input.KeyEventArgs e) {
            if (e.Key == Key.H && Keyboard.Modifiers == ModifierKeys.Control) {
                _visibleTools = !_visibleTools;
                UpdateVisibility(true);
                e.Handled = true;
            }
        }

        protected override void OnResize(object sender, EventArgs e) {
            base.OnResize(sender, e);
            UpdatePosition();
            Save();
        }

        private void OnMove(object sender, EventArgs e) {
            UpdatePosition();
            Save();
        }

        private void Tools_Activated(object sender, EventArgs e) {
            UpdateVisibility(Form.Focused);
        }

        private void Tools_Deactivated(object sender, EventArgs e) {
            UpdateVisibility(Form.Focused);
        }

        private void Tools_Closed(object sender, EventArgs e) {
            _tools = null;
        }

        private void OnClosed(object sender, EventArgs e) {
            if (_tools != null) {
                _tools.Close();
                _tools = null;
            }
        }

        private bool _updating;

        private async void UpdateVisibility(bool keepFocus) {
            if (_updating) return;

            _updating = true;
            await Task.Delay(1);

            if (_tools != null) {
                var val = !EditMode && _visibleTools && (Form.Focused || _tools.IsActive) ? Visibility.Visible : Visibility.Hidden;
                if (val != _tools.Visibility) {
                    _tools.Visibility = val;
                    if (val == Visibility.Visible) {
                        _tools.Topmost = false;
                        _tools.Topmost = true;
                    }

                    if (keepFocus) {
                        await Task.Delay(1);
                        Form.Focus();
                        Form.Activate();
                    }
                }
            }

            _updating = false;
        }

        protected override void OnGotFocus(object sender, EventArgs e) {
            base.OnGotFocus(sender, e);
            UpdateVisibility(true);
        }

        protected override void OnLostFocus(object sender, EventArgs e) {
            base.OnLostFocus(sender, e);
            UpdateVisibility(false);
        }
    }
}