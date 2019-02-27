using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using System.Windows.Media;
using AcManager.Controls.Presentation;
using AcTools.Render.Base;
using AcTools.Render.Wrapper;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using SlimDX.Windows;
using CheckBox = System.Windows.Controls.CheckBox;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using TextBoxBase = System.Windows.Controls.Primitives.TextBoxBase;

namespace AcManager.CustomShowroom {
    internal class AttachedHelper {
        public static bool OptionAutoHideTools = true;

        private static readonly WeakList<AttachedHelper> Instances = new WeakList<AttachedHelper>();

        [CanBeNull]
        public static AttachedHelper GetInstance(Window window) {
            Instances.Purge();
            return Instances.FirstOrDefault(x => ReferenceEquals(x._child, window));
        }

        [CanBeNull]
        public static AttachedHelper GetInstance(BaseRenderer renderer) {
            Instances.Purge();
            return Instances.FirstOrDefault(x => ReferenceEquals(x._wrapper.Renderer, renderer));
        }

        private readonly FormWrapperBase _wrapper;
        private readonly RenderForm _parent;

        [CanBeNull]
        private DpiAwareWindow _child;

        private readonly List<Window> _attached = new List<Window>();

        private bool _visible;

        public bool Visible {
            get => _visible;
            set {
                if (Equals(_visible, value)) return;
                _visible = value;
                UpdateVisibility(true);
            }
        }

        private readonly int _padding;
        private readonly bool _limitHeight;
        private readonly bool _verbose;
        private readonly int _offset;

        public static bool OptionInteropMode = true;

        public AttachedHelper([NotNull] FormWrapperBase parent, [NotNull] DpiAwareWindow child, int offset = -1, int padding = 10, bool limitHeight = true,
                bool verbose = false) {
            _padding = padding;
            _limitHeight = limitHeight;
            _verbose = verbose;

            _wrapper = parent;
            _parent = parent.Form;
            _parent.Closed += OnClosed;
            _parent.UserResized += OnResize;
            _parent.Move += OnMove;
            _parent.Load += OnLoad;
            _parent.GotFocus += OnGotFocus;
            _parent.LostFocus += OnLostFocus;
            parent.FullscreenChanged += OnFullscreenChanged;

            if (_verbose) {
                Logging.Here();
            }

            _child = child;
            _stickyLocation = Stored.Get(child.LocationAndSizeKey + ":sticky", 0);

            child.Owner = null;
            if (_limitHeight) {
                child.MaxHeight = _parent.Height - _padding;
            }

            if (OptionInteropMode) {
                child.SetOwnerWindowAndFocus(_parent.Handle, true);
            }
            ElementHost.EnableModelessKeyboardInterop(child);

            if (_verbose) {
                Logging.Debug("Showing and activating child…");
            }

            UpdateStyle(child);
            _offset = offset;

            child.Closing += OnChildClosing;
            child.Activated += ChildActivated;
            child.Deactivated += ChildDeactivated;
            child.Closed += ChildClosed;
            child.KeyUp += ChildKeyUp;
            child.LocationChanged += ChildLocationChanged;
            child.SizeChanged += ChildSizeChanged;

            Instances.Purge();
            Instances.Add(this);

            parent.Form.Load += OnFormLoad;
        }

        private void UpdateStyle(DpiAwareWindow window) {
            window.ConsiderPreferredFullscreen = false;
            window.ToolWindow = true;
            window.WindowStyle = WindowStyle.None;

            if (AppAppearanceManager.Instance.SemiTransparentAttachedTools) {
                window.AllowsTransparency = true;
                window.BlurBackground = true;
                window.Opacity = 0.9;
                window.BorderBrush = new SolidColorBrush(Colors.Transparent);
                window.Background = new SolidColorBrush(((Color)window.FindResource("WindowBackgroundColor")).SetAlpha(200));
            }
        }

        private void OnFormLoad(object o, EventArgs eventArgs) {
            _moveChildBusy.Delay(500);

            var child = _child;
            if (child == null) return;

            child.Show();
            child.Activate();

            Visible = true;
            RepositionChild();

            if (!OptionInteropMode) {
                child.Topmost = true;
                child.Activate();
            }
        }

        private async void RepositionChild() {
            await Task.Yield();
            UpdatePosition(true);
        }

        private bool _closed;

        private void OnChildClosing(object o, CancelEventArgs cancelEventArgs) {
            if (!_closed) {
                cancelEventArgs.Cancel = true;
            }
        }

        private void OnClosed(object sender, EventArgs e) {
            _closed = true;

            if (_child != null) {
                _child.Close();
                _child = null;
            }

            foreach (var window in _attached.ToList()) {
                try {
                    window.Close();
                } catch {
                    // ignored
                }
            }

            _attached.Clear();
        }

        private void OnResize(object sender, EventArgs e) {
            UpdatePosition();
            if (_child != null && Visible && _limitHeight) {
                //_child.MaxHeight = _parent.Height - _padding;
            }
        }

        private void OnMove(object sender, EventArgs e) {
            UpdatePosition();
        }

        private void OnFullscreenChanged(object sender, EventArgs e) {
            UpdatePosition();
        }

        private readonly Busy _moveChildBusy = new Busy();

        private void ChildLocationChanged(object sender, EventArgs e) {
            var child = _child;
            if (child == null) return;
            _moveChildBusy.Do(() => {
                var pos = new Point(child.DeviceLeft, child.DeviceTop);
                for (var i = 0; i < StickyLocationsCount; i++) {
                    var location = GetStickyLocation(i, child.DeviceWidth, child.DeviceHeight);
                    if (location.HasValue && (pos - location.Value).Length < 5) {
                        _stickyLocation.Value = i;
                        UpdatePosition(true);
                        return;
                    }
                }

                _stickyLocation.Value = -1;
            });
        }

        private void ChildSizeChanged(object sender, SizeChangedEventArgs e) {
            if (_child == null) return;
            UpdatePosition(_child.DeviceScaleX * e.NewSize.Width, _child.DeviceScaleY * e.NewSize.Height, true);
        }

        private void ChildKeyUp(object sender, KeyEventArgs e) {
            if (Keyboard.FocusedElement is TextBoxBase || Keyboard.FocusedElement is CheckBox) {
                return;
            }

            switch (e.Key) {
                case Key.PageDown:
                case Key.PageUp:
                    return;
            }

            if (e.Key == Key.H && Keyboard.Modifiers == ModifierKeys.Control) {
                _visible = !_visible;
                UpdateVisibility(true);
                e.Handled = true;
            }

            if (!e.Handled) {
                var key = (Keys)KeyInterop.VirtualKeyFromKey(e.Key);
                var modifiers = Keyboard.Modifiers;
                if ((modifiers & ModifierKeys.Alt) != 0) key |= Keys.Alt;
                if ((modifiers & ModifierKeys.Control) != 0) key |= Keys.Control;
                if ((modifiers & ModifierKeys.Shift) != 0) key |= Keys.Shift;
                var args = new System.Windows.Forms.KeyEventArgs(key);
                _wrapper.OnKeyUp(args);
                e.Handled = args.Handled;
            }
        }

        private void UpdatePosition(double w, double h, bool force) {
            if (!AppearanceManager.Instance.ManageWindowsLocation) return;

            if (_verbose) {
                Logging.Here();
            }

            var n = GetStickyLocation(_stickyLocation.Value, w, h);
            if (_verbose) {
                Logging.Debug($"Sticky position: {n?.X ?? -1}, {n?.Y ?? -1}, #{_stickyLocation.Value}");
            }

            if (n == null || _child == null) {
                if (_verbose) {
                    Logging.Debug("Nothing to do");
                }

                return;
            }

            var location = n.Value;

            Screen screen;
            try {
                screen = Screen.FromControl(_parent);
            } catch (Exception) {
                return;
            }

            if (location.X + _child.DeviceWidth > screen.WorkingArea.Right) {
                location.X = screen.WorkingArea.Right - _child.DeviceWidth;
            }

            if (location.Y + _child.DeviceHeight > screen.WorkingArea.Bottom) {
                location.Y = screen.WorkingArea.Bottom - _child.DeviceHeight;
            }

            if (location.X < screen.WorkingArea.Left) {
                location.X = screen.WorkingArea.Left;
            }

            if (location.Y < screen.WorkingArea.Top) {
                location.Y = screen.WorkingArea.Top;
            }

            if (_verbose) {
                Logging.Debug($"Set: {location.X}, {location.Y}");
            }

            if (force) {
                Apply();
            } else {
                _moveChildBusy.Do(Apply);
            }

            void Apply() {
                if (_child == null || !_child.IsLoaded) return;
                _child.DeviceLeft = location.X;
                _child.DeviceTop = location.Y;
            }
        }

        private void UpdatePosition(bool force = false) {
            if (_verbose) {
                Logging.Debug("Child: " + _child);
            }

            if (_child == null) return;
            UpdatePosition(_child.DeviceWidth, _child.DeviceHeight, force);
        }

        private void ChildActivated(object sender, EventArgs e) {
            if (OptionInteropMode) return;
            if (_verbose) {
                Logging.Debug("Child activated: " + _child);
            }

            _busyGotFocus.DoDelay(() => UpdateVisibility(_parent.Focused), 200);
        }

        private void ChildDeactivated(object sender, EventArgs e) {
            if (OptionInteropMode) return;
            if (_verbose) {
                Logging.Debug("Child deactivated: " + _child);
            }

            UpdateVisibility(_parent.Focused);
        }

        private void ChildClosed(object sender, EventArgs e) {
            if (_verbose) {
                Logging.Warning("Child closed: " + _child);
            }

            _child = null;
            Instances.Remove(this);
        }

        private readonly Busy _busyUpdating = new Busy();

        private bool IsAnyActive() {
            return !OptionAutoHideTools || (_parent.Focused || _child?.IsActive == true || _attached.Any(x => x.IsActive));
        }

        private Visibility GetVisibility() {
            return _visible && IsAnyActive() ? Visibility.Visible : Visibility.Hidden;
        }

        private async Task UpdateVisibility(Window child, Visibility visibility, bool setFocus) {
            if (OptionInteropMode) return;
            if (_verbose) {
                Logging.Debug("Value: " + visibility + "; set focus: " + setFocus);
            }

            if (child == null) {
                if (_verbose) {
                    Logging.Warning("No child to update");
                }
                return;
            }

            if (visibility != child.Visibility) {
                if (_verbose) {
                    Logging.Here();
                }

                child.Visibility = visibility;

                if (visibility == Visibility.Visible) {
                    child.Topmost = false;
                    child.Topmost = true;
                }

                if (setFocus) {
                    await Task.Delay(1);
                    _parent.Focus();
                    _parent.Activate();
                }
            } else if (_verbose) {
                Logging.Debug("Nothing to do");
            }
        }

        private void UpdateVisibility(bool keepFocus) {
            if (OptionInteropMode) return;
            if (_verbose) {
                Logging.Debug($"Update visibility; keep focus: {keepFocus}, is busy: {_busyUpdating.Is}");
            }

            _busyUpdating.DoDelay(async () => {
                var visibility = GetVisibility();
                await UpdateVisibility(_child, visibility, keepFocus);
                foreach (var window in _attached) {
                    await UpdateVisibility(window, visibility, false);
                }
            }, 1);
        }

        private Busy _busyGotFocus = new Busy();

        protected void OnGotFocus(object sender, EventArgs e) {
            if (OptionInteropMode) return;
            if (_verbose) {
                Logging.Here();
            }

            UpdateVisibility(true);
        }

        protected void OnLostFocus(object sender, EventArgs e) {
            if (OptionInteropMode) return;
            if (_verbose) {
                Logging.Here();
            }

            UpdateVisibility(false);
        }

        public void Attach(string tag, DpiAwareWindow window) {
            if (_verbose) {
                Logging.Debug($"Tag: {tag}, window: {window}");
            }

            if (tag != null) {
                try {
                    _attached.FirstOrDefault(x => Equals(x.Tag, tag))?.Close();
                } catch {
                    // ignored
                }
            }

            UpdateStyle(window);
            _attached.Add(window);
            window.Owner = null;
            window.Activated += ChildActivated;
            window.Deactivated += ChildDeactivated;
            window.Closed += OnWindowClosed;
            window.Tag = tag;

            if (OptionInteropMode) {
                if (_child != null) {
                    window.Owner = _child;
                } else {
                    window.SetOwnerWindowAndFocus(_parent.Handle, true);
                }
                window.Show();
                window.Activate();
            } else {
                window.Show();
                window.Activate();
                window.Topmost = true;
                UpdateVisibility(window, GetVisibility(), true).Forget();
            }

            ElementHost.EnableModelessKeyboardInterop(window);
        }

        public Task AttachAndWaitAsync(string tag, DpiAwareWindow window) {
            if (_verbose) {
                Logging.Debug($"Tag: {tag}, window: {window}");
            }

            if (tag != null) {
                try {
                    _attached.FirstOrDefault(x => Equals(x.Tag, tag))?.Close();
                } catch {
                    // ignored
                }
            }

            UpdateStyle(window);
            _attached.Add(window);
            window.Owner = null;
            window.Activated += ChildActivated;
            window.Deactivated += ChildDeactivated;
            window.Closed += OnWindowClosed;
            window.Tag = tag;

            if (OptionInteropMode) {
                if (_child != null) {
                    window.Owner = _child;
                } else {
                    window.SetOwnerWindowAndFocus(_parent.Handle, true);
                }
                window.Show();
                window.Activate();
            } else {
                window.Show();
                window.Activate();
                window.Topmost = true;
                UpdateVisibility(window, GetVisibility(), true).Forget();
            }

            ElementHost.EnableModelessKeyboardInterop(window);

            var tcs = new TaskCompletionSource<bool>();
            window.Closed += (sender, args) => {
                tcs.SetResult(true);
                if (_child == null) {
                    _parent.Activate();
                }
            };
            return tcs.Task;
        }

        private void OnWindowClosed(object sender, EventArgs eventArgs) {
            if (_verbose) {
                Logging.Here();
            }

            _attached.Remove((Window)sender);
        }

        private readonly StoredValue<int> _stickyLocation;
        private const int StickyLocationsCount = 4;

        private Point? GetStickyLocation(int index, double w, double h) {
            if (_child == null) return null;
            var offset = _offset < 0 ? (int)_child.DeviceWidth - 12 : _offset;
            switch (index) {
                case 0:
                    return new Point(_parent.Left + _parent.Width - w + offset, _parent.Top + _parent.Height - h - _padding);
                case 1:
                    return new Point(_parent.Left - offset, _parent.Top + _parent.Height - h - _padding);
                case 2:
                    return new Point(_parent.Left + _parent.Width - w + offset, _parent.Top - _padding);
                case 3:
                    return new Point(_parent.Left - offset, _parent.Top - _padding);
                default:
                    return null;
            }
        }

        private void OnLoad(object sender, EventArgs e) {
            if (_verbose) {
                Logging.Here();
            }

            UpdateVisibility(true);
        }

        public bool IsActive => _child?.IsActive == true;
    }
}