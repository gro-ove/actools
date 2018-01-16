using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Forms;
using AcManager.Tools.Objects;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Utils;
using AcTools.Windows;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Serialization;
using SlimDX;
using Size = System.Drawing.Size;
using KeyEventArgs = System.Windows.Forms.KeyEventArgs;

namespace AcManager.CustomShowroom {
    public class LiteShowroomFormWrapperWithTools : LiteShowroomFormWrapperWithUiShots, ICustomShowroomShots {
        private readonly AttachedHelper _helper;
        private readonly LiteShowroomTools _tools;

        public new ToolsKn5ObjectRenderer Kn5ObjectRenderer => (ToolsKn5ObjectRenderer)Renderer;

        public LiteShowroomFormWrapperWithTools(ToolsKn5ObjectRenderer renderer, CarObject car, string skinId, string presetFilename)
                : base(renderer, car.DisplayName) {
            _tools = new LiteShowroomTools(renderer, car, skinId, presetFilename, this);
            _helper = new AttachedHelper(this, _tools, limitHeight: false);
            GoToNormalMode();

            renderer.VisibleUi = false;
            Form.Move += OnMove;
        }

        protected override void OnClickOverride() {
            if (_tools.CanSelectNodes && !User32.IsKeyPressed(Keys.LControlKey) && !User32.IsKeyPressed(Keys.RControlKey)) {
                Kn5ObjectRenderer.OnClick(new Vector2(MousePosition.X, MousePosition.Y));
            }
        }

        public Size DefaultSize {
            get {
                var screen = Screen.FromControl(Form);
                return new Size(screen.Bounds.Width, screen.Bounds.Height);
            }
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
                var size = ValuesStorage.Get(KeyNormalSize, new Point(1600, 900));
                var pos = ValuesStorage.Get(KeyNormalPos, new Point((area.Width - size.X) / 2, (area.Height - size.Y) / 2));

                Form.Width = ((int)size.X).Clamp(320, area.Width);
                Form.Height = ((int)size.Y).Clamp(200, area.Height);
                Form.Top = ((int)pos.Y).Clamp(0, area.Height - Form.Height);
                Form.Left = ((int)pos.X).Clamp(0, area.Width - Form.Width);

                Form.WindowState = ValuesStorage.Get(KeyNormalMaximized, false) ? FormWindowState.Maximized : FormWindowState.Normal;
                Form.FormBorderStyle = FormBorderStyle.Sizable;
                Form.TopMost = false;
                FullscreenEnabled = ValuesStorage.Get(KeyNormalFullscreen, false);

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
                var size = ValuesStorage.Get(KeyToolSize, new Point(400, 240));
                var pos = ValuesStorage.Get(KeyToolPos, new Point(80, Screen.PrimaryScreen.WorkingArea.Height - 300));

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
                ValuesStorage.Set(KeyToolSize, new Point(Form.Width, Form.Height).As<string>());
                ValuesStorage.Set(KeyToolPos, new Point(Form.Left, Form.Top).As<string>());
            } else {
                ValuesStorage.Set(KeyNormalFullscreen, FullscreenEnabled);

                if (FullscreenEnabled) {
                    ValuesStorage.Set(KeyNormalMaximized, false);
                } else {
                    ValuesStorage.Set(KeyNormalMaximized, Form.WindowState == FormWindowState.Maximized);
                    if (Form.WindowState == FormWindowState.Normal) {
                        ValuesStorage.Set(KeyNormalSize, new Point(Form.Width, Form.Height).As<string>());
                        ValuesStorage.Set(KeyNormalPos, new Point(Form.Left, Form.Top).As<string>());
                    }
                }
            }
        }

        public event EventHandler<CancelEventArgs> PreviewScreenshot;

        protected override void OnKeyUpOverride(KeyEventArgs args) {
            switch (args.KeyCode) {
                case Keys.F8:
                    var cancelEventArgs = new CancelEventArgs();
                    PreviewScreenshot?.Invoke(this, cancelEventArgs);
                    if (cancelEventArgs.Cancel) {
                        args.Handled = true;
                    }
                    break;

                case Keys.H:
                    if (args.Alt) {
                        if (Renderer is ToolsKn5ObjectRenderer tools) {
                            if (!args.Control && !args.Shift) {
                                tools.ToggleSelected();
                                args.Handled = true;
                            } else if (!args.Control && args.Shift) {
                                tools.UnhideAll();
                                args.Handled = true;
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
                        args.Handled = true;
                    }
                    break;

                case Keys.Escape:
                    if (Kn5ObjectRenderer.SelectedObject != null) {
                        Kn5ObjectRenderer.Deselect();
                        args.Handled = true;
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
                        args.Handled = true;
                    }
                    break;
            }
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