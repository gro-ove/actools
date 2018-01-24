using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using AcTools.Render.Base;
using AcTools.Render.Wrapper;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;
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

        public AttachedHelper([NotNull] FormWrapperBase parent, [NotNull] DpiAwareWindow window, int offset = -1, int padding = 10, bool limitHeight = true,
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

            _child = window;
            var child = _child;

            child.Owner = null;
            if (_limitHeight) {
                child.MaxHeight = _parent.Height - _padding;
            }

            ElementHost.EnableModelessKeyboardInterop(child);

            if (_verbose) {
                Logging.Debug("Showing and activating child…");
            }

            child.Show();
            child.Activate();

            _offset = offset < 0 ? (int)child.ActualWidth - 12 : offset;

            UpdatePosition();

            child.Closing += OnChildClosing;
            child.WindowStyle = WindowStyle.ToolWindow;
            child.Activated += ChildActivated;
            child.Deactivated += ChildDeactivated;
            child.Closed += ChildClosed;
            child.PreviewKeyDown += ChildKeyDown;
            child.KeyUp += ChildKeyUp;
            child.LocationChanged += ChildLocationChanged;
            child.SizeChanged += ChildSizeChanged;

            child.Topmost = true;
            Visible = true;

            Instances.Purge();
            Instances.Add(this);
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
                _child.MaxHeight = _parent.Height - _padding;
            }
        }

        private void OnMove(object sender, EventArgs e) {
            UpdatePosition();
        }

        private void OnFullscreenChanged(object sender, EventArgs e) {
            UpdatePosition();
        }

        private bool _skip;

        private void ChildLocationChanged(object sender, EventArgs e) {
            if (_skip || _child == null) return;

            var pos = new Point(_child.Left, _child.Top);
            foreach (var i in from i in Enumerable.Range(0, StickyLocationsCount)
                              let location = GetStickyLocation(i, _child.Width, _child.Height)
                              where location.HasValue
                              let delta = pos - location.Value
                              where delta.Length < 5
                              select i) {
                _stickyLocation = i;
                UpdatePosition();
                return;
            }

            _stickyLocation = -1;
        }

        private void ChildSizeChanged(object sender, SizeChangedEventArgs e) {
            UpdatePosition(e.NewSize.Width, e.NewSize.Height);
        }

        private void ChildKeyDown(object sender, KeyEventArgs e) {}

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

        private void UpdatePosition(double w, double h) {
            if (_verbose) {
                Logging.Here();
            }

            var n = GetStickyLocation(_stickyLocation, w, h);
            if (_verbose) {
                Logging.Debug($"Sticky position: {n?.X ?? -1}, {n?.Y ?? -1}");
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

            if (location.X + _child.Width > screen.Bounds.Right) {
                location.X = screen.Bounds.Right - _child.Width;
            }

            if (location.Y + _child.Height > screen.Bounds.Bottom) {
                location.Y = screen.Bounds.Bottom - _child.Height;
            }

            if (location.X < screen.Bounds.Left) location.X = screen.Bounds.Left;
            if (location.Y < screen.Bounds.Top) location.Y = screen.Bounds.Top;

            _skip = true;

            if (_verbose) {
                Logging.Debug($"Set: {location.X}, {location.Y}");
            }

            _child.Left = location.X;
            _child.Top = location.Y;
            _skip = false;
        }

        private void UpdatePosition() {
            if (_verbose) {
                Logging.Debug("Child: " + _child);
            }

            if (_child == null) return;
            UpdatePosition(_child.Width, _child.Height);
        }

        private void ChildActivated(object sender, EventArgs e) {
            if (_verbose) {
                Logging.Debug("Child activated: " + _child);
            }

            UpdateVisibility(_parent.Focused);
        }

        private void ChildDeactivated(object sender, EventArgs e) {
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
            Logging.Debug($"Update visibility; keep focus: {keepFocus}, is busy: {_busyUpdating.Is}");
            _busyUpdating.DoDelay(async () => {
                var visibility = GetVisibility();
                await UpdateVisibility(_child, visibility, keepFocus);
                foreach (var window in _attached) {
                    await UpdateVisibility(window, visibility, false);
                }
            }, 1);
        }

        protected void OnGotFocus(object sender, EventArgs e) {
            if (_verbose) {
                Logging.Here();
            }

            UpdateVisibility(true);
        }

        protected void OnLostFocus(object sender, EventArgs e) {
            if (_verbose) {
                Logging.Here();
            }

            UpdateVisibility(false);
        }

        public void Attach(string tag, Window window) {
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

            _attached.Add(window);
            window.Owner = null;
            window.Activated += ChildActivated;
            window.Deactivated += ChildDeactivated;
            window.Closed += OnWindowClosed;
            window.Tag = tag;
            window.Show();
            window.Activate();
            window.Topmost = true;
            ElementHost.EnableModelessKeyboardInterop(window);
            UpdateVisibility(window, GetVisibility(), true).Forget();
        }

        public void Attach(Window window) {
            Attach(null, window);
        }

        public Task AttachAndWaitAsync(string tag, Window window) {
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

            _attached.Add(window);
            window.Owner = null;
            window.Activated += ChildActivated;
            window.Deactivated += ChildDeactivated;
            window.Closed += OnWindowClosed;
            window.Tag = tag;
            window.Show();
            window.Activate();
            window.Topmost = true;
            ElementHost.EnableModelessKeyboardInterop(window);
            UpdateVisibility(window, GetVisibility(), true).Forget();

            var tcs = new TaskCompletionSource<bool>();
            window.Closed += (sender, args) => tcs.SetResult(true);
            return tcs.Task;
        }

        public Task AttachAndWaitAsync(Window window) {
            return AttachAndWaitAsync(null, window);
        }

        private void OnWindowClosed(object sender, EventArgs eventArgs) {
            if (_verbose) {
                Logging.Here();
            }

            _attached.Remove((Window)sender);
        }

        private int _stickyLocation;
        private const int StickyLocationsCount = 4;

        private Point? GetStickyLocation(int index, double w, double h) {
            switch (index) {
                case 0:
                    return new Point(_parent.Left + _parent.Width - w + _offset, _parent.Top + _parent.Height - h - _padding);
                case 1:
                    return new Point(_parent.Left - _offset, _parent.Top + _parent.Height - h - _padding);
                case 2:
                    return new Point(_parent.Left + _parent.Width - w + _offset, _parent.Top - _padding);
                case 3:
                    return new Point(_parent.Left - _offset, _parent.Top - _padding);
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