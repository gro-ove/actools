using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using AcManager.Tools.Objects;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Render.Wrapper;
using KeyEventArgs = System.Windows.Forms.KeyEventArgs;

namespace AcManager.Controls.CustomShowroom {
    public class LiteShowroomWrapperWithTools : LiteShowroomWrapper {
        private LiteShowroomTools _tools;
        private bool _visibleTools = true;

        public LiteShowroomWrapperWithTools(ForwardKn5ObjectRenderer renderer, CarObject car, string skinId) : base(renderer) {
            Form.Closed += OnClosed;
            renderer.VisibleUi = false;

            Form.Move += OnMove;

            _tools = new LiteShowroomTools(renderer, car, skinId);
            _tools.Show();

            UpdatePosition();

            _tools.Activated += Tools_Activated;
            _tools.Deactivated += Tools_Deactivated;
            _tools.Closed += Tools_Closed;
            _tools.KeyUp += Tools_KeyUp;

            Form.Load += OnLoad;
        }

        private void UpdatePosition() {
            _tools.Left = Form.Left + Form.Width - _tools.Width + 20;
            _tools.Top = Form.Top + Form.Height - _tools.Height - 20;
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
        }

        private void OnMove(object sender, EventArgs e) {
            UpdatePosition();
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
                var val = _visibleTools && (Form.Focused || _tools.IsActive) ? Visibility.Visible : Visibility.Hidden;
                if (val != _tools.Visibility) {
                    _tools.Visibility = val;
                    if (val == Visibility.Visible) {
                        _tools.Topmost = false;
                        _tools.Topmost = true;
                    }

                    if (keepFocus) {
                        Form.Focus();
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