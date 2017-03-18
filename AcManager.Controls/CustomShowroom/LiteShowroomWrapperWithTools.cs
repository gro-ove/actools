using System;
using System.Windows;
using System.Windows.Forms;
using AcManager.Tools.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Render.Wrapper;
using AcTools.Utils;
using FirstFloor.ModernUI.Helpers;
using SlimDX;
using KeyEventArgs = System.Windows.Forms.KeyEventArgs;

namespace AcManager.Controls.CustomShowroom {
    public class LiteShowroomWrapperWithTools : LiteShowroomWrapper {
        private readonly AttachedHelper _helper;

        public new ToolsKn5ObjectRenderer Kn5ObjectRenderer => (ToolsKn5ObjectRenderer)Renderer;

        public LiteShowroomWrapperWithTools(ToolsKn5ObjectRenderer renderer, CarObject car, string skinId) : base(renderer, car.DisplayName) {
            _helper = new AttachedHelper(this, new LiteShowroomTools(renderer, car, skinId));
            GoToNormalMode();

            renderer.VisibleUi = false;
            Form.Move += OnMove;
        }

        protected override void OnClick() {
            base.OnClick();
            Kn5ObjectRenderer.OnClick(new Vector2(MousePosition.X, MousePosition.Y));
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

                Renderer.Width = Form.ClientSize.Width;
                Renderer.Height = Form.ClientSize.Height;

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

                FullscreenEnabled = false;
                Form.WindowState = FormWindowState.Normal;
                Form.Width = ((int)size.X).Clamp(320, area.Width);
                Form.Height = ((int)size.Y).Clamp(200, area.Height);
                Form.Top = ((int)pos.Y).Clamp(0, area.Height - Form.Height);
                Form.Left = ((int)pos.X).Clamp(0, area.Width - Form.Width);

                Form.FormBorderStyle = FormBorderStyle.SizableToolWindow;
                Form.TopMost = true;

                Renderer.Width = Form.ClientSize.Width;
                Renderer.Height = Form.ClientSize.Height;

                _lastVisibleTools = _helper.Visible;
                _helper.Visible = false;
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

        protected override void OnKeyUp(object sender, KeyEventArgs args) {
            switch (args.KeyCode) {
                case Keys.H:
                    if (args.Control && !args.Alt && !args.Shift) {
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

        protected override void OnRender() {
            if (Renderer == null || Paused && !_helper.IsActive && !Renderer.IsDirty) return;
            Renderer.Draw();
        }

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