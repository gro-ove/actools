using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using AcManager.Tools.Objects;
using AcTools.Render.Kn5SpecificForward;
using AcTools.Utils;
using AcTools.Windows;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using SlimDX;
using Size = System.Drawing.Size;
using KeyEventArgs = System.Windows.Forms.KeyEventArgs;
using Point = System.Windows.Point;

namespace AcManager.CustomShowroom {
    public class LiteShowroomFormWrapperWithTools : LiteShowroomFormWrapperWithUiShots, ICustomShowroomShots {
        public static bool OptionAttachedToolsLogging = false;

        private readonly AttachedHelper _helper;
        private readonly LiteShowroomTools _tools;

        public new ToolsKn5ObjectRenderer Kn5ObjectRenderer => (ToolsKn5ObjectRenderer)Renderer;

        public LiteShowroomFormWrapperWithTools(ToolsKn5ObjectRenderer renderer, CarObject car, string skinId, string presetFilename)
                : base(renderer, car.DisplayName) {
            if (OptionAttachedToolsLogging) {
                Logging.Here();
            }

            _tools = new LiteShowroomTools(renderer, car, skinId, presetFilename, this, OptionAttachedToolsLogging);
            _helper = new AttachedHelper(this, _tools, limitHeight: false, verbose: OptionAttachedToolsLogging);
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

        private Rectangle GetScreenBounds() {
            return (Form.Visible ? Screen.FromControl(Form) : DpiAwareWindow.GetPreferredScreen()).Bounds;
        }

        protected sealed override void GoToNormalMode() {
            if (OptionAttachedToolsLogging) {
                Logging.Debug("Switching to normal mode…");
            }

            _switchingInProgress = true;

            try {
                if (AppearanceManager.Current.PreferFullscreenMode) {
                    var screen = DpiAwareWindow.GetPreferredScreen();
                    Form.Width = screen.Bounds.Width;
                    Form.Height = screen.Bounds.Height;
                    Form.Top = screen.Bounds.Top;
                    Form.Left = screen.Bounds.Left;
                    FullscreenEnabled = true;
                } else {
                    var size = ValuesStorage.Get(KeyNormalSize, default(Point));
                    var pos = ValuesStorage.Get(KeyNormalPos, default(Point));

                    if (size.X > 0 && size.Y > 0) {
                        var savedScreen = pos != default(Point) ? Screen.FromPoint(new System.Drawing.Point(
                                (int)(pos.X + size.X / 2), (int)(pos.Y + size.Y / 2))) : DpiAwareWindow.GetPreferredScreen();
                        var activeScreen = AppearanceManager.Current.KeepWithinSingleScreen ? DpiAwareWindow.GetActiveScreen() : null;
                        if (activeScreen != null && savedScreen.Bounds != activeScreen.Bounds) {
                            SetDefaultLocation();
                        } else {
                            Form.Width = size.X.RoundToInt().Clamp(320, savedScreen.Bounds.Width);
                            Form.Height = size.Y.RoundToInt().Clamp(200, savedScreen.Bounds.Height);
                            Form.Top = pos.Y.RoundToInt().Clamp(savedScreen.Bounds.Top, savedScreen.Bounds.Bottom - Form.Height);
                            Form.Left = pos.X.RoundToInt().Clamp(savedScreen.Bounds.Left, savedScreen.Bounds.Right - Form.Width);
                        }
                    } else {
                        SetDefaultLocation();
                    }

                    if (_lastVisibleTools.HasValue) {
                        _helper.Visible = _lastVisibleTools.Value;
                    }

                    FullscreenEnabled = ValuesStorage.Get(KeyNormalFullscreen, false);
                }

                void SetDefaultLocation() {
                    var screen = DpiAwareWindow.GetPreferredScreen();
                    Form.Width = 1600.Clamp(320, screen.Bounds.Width);
                    Form.Height = 900.Clamp(200, screen.Bounds.Height);
                    Form.Top = screen.Bounds.Top + (screen.Bounds.Height - Form.Height) / 2;
                    Form.Left = screen.Bounds.Left + (screen.Bounds.Width - Form.Width) / 2;
                }

                UpdateSize();
            } finally {
                _switchingInProgress = false;
            }

            if (OptionAttachedToolsLogging) {
                Logging.Here();
            }
        }

        protected override void GoToToolMode() {
            if (OptionAttachedToolsLogging) {
                Logging.Debug("Switching to tool mode…");
            }

            _switchingInProgress = true;

            try {
                var area = GetScreenBounds();
                var size = ValuesStorage.Get(KeyToolSize, new Point(400, 240));
                var pos = ValuesStorage.Get(KeyToolPos, new Point(80, Screen.PrimaryScreen.WorkingArea.Height - 300));

                _lastVisibleTools = _helper.Visible;
                _helper.Visible = false;

                FullscreenEnabled = false;
                Form.WindowState = FormWindowState.Normal;
                Form.Width = ((int)size.X).Clamp(320, area.Width);
                Form.Height = ((int)size.Y).Clamp(200, area.Height);
                Form.Top = ((int)pos.Y).Clamp(area.Top, area.Bottom - Form.Height);
                Form.Left = ((int)pos.X).Clamp(area.Left, area.Right - Form.Width);
                Form.FormBorderStyle = FormBorderStyle.SizableToolWindow;
                Form.TopMost = true;
                UpdateSize();
            } finally {
                _switchingInProgress = false;
            }

            if (OptionAttachedToolsLogging) {
                Logging.Here();
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

        public event EventHandler<CancelEventArgs> PreviewScreenshot;

        protected override void OnKeyUpOverride(KeyEventArgs args) {
            switch (args.KeyCode) {
                case Keys.F11:
                    if (AppearanceManager.Current.PreferFullscreenMode) {
                        args.Handled = true;
                    }
                    break;

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