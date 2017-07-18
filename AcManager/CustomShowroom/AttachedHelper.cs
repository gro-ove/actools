using System;
using System.Collections.Generic;
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
using JetBrains.Annotations;
using SlimDX.Windows;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace AcManager.CustomShowroom {
    internal class AttachedHelper {
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
        private Window _child;
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
        private readonly int _offset;

        public AttachedHelper([NotNull] FormWrapperBase parent, [NotNull] Window window, int offset = -1, int padding = 10, bool limitHeight = true) {
            _padding = padding;
            _limitHeight = limitHeight;

            _wrapper = parent;
            _parent = parent.Form;
            _parent.Closed += OnClosed;
            _parent.UserResized += OnResize;
            _parent.Move += OnMove;
            _parent.Load += OnLoad;
            _parent.GotFocus += OnGotFocus;
            _parent.LostFocus += OnLostFocus;
            parent.FullscreenChanged += OnFullscreenChanged;

            _child = window;
            var child = _child;

            child.Owner = null;
            if (_limitHeight) {
                child.MaxHeight = _parent.Height - _padding;
            }

            ElementHost.EnableModelessKeyboardInterop(child);

            child.Show();
            child.Activate();

            _offset = offset < 0 ? (int)child.ActualWidth - 12 : offset;

            UpdatePosition();

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

        private void OnClosed(object sender, EventArgs e) {
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
            var n = GetStickyLocation(_stickyLocation, w, h);
            if (n == null || _child == null) return;

            var location = n.Value;

            Screen screen;
            try {
                screen = Screen.FromControl(_parent);
            } catch (Exception) {
                return;
            }

            if (location.X + _child.Width > screen.Bounds.Width) {
                location.X = screen.Bounds.Width - _child.Width;
            }

            if (location.Y + _child.Height > screen.Bounds.Height) {
                location.Y = screen.Bounds.Height - _child.Height;
            }

            if (location.X < 0) location.X = 0;
            if (location.Y < 0) location.Y = 0;

            _skip = true;
            _child.Left = location.X;
            _child.Top = location.Y;
            _skip = false;
        }

        private void UpdatePosition() {
            if (_child == null) return;
            UpdatePosition(_child.Width, _child.Height);
        }

        private void ChildActivated(object sender, EventArgs e) {
            UpdateVisibility(_parent.Focused);
        }

        private void ChildDeactivated(object sender, EventArgs e) {
            UpdateVisibility(_parent.Focused);
        }

        private void ChildClosed(object sender, EventArgs e) {
            _child = null;
            Instances.Remove(this);
        }

        private readonly Busy _busyUpdating = new Busy();

        private bool IsAnyActive() {
            return _parent.Focused || _child?.IsActive == true || _attached.Any(x => x.IsActive);
        }

        private Visibility GetVisibility() {
            return _visible && IsAnyActive() ? Visibility.Visible : Visibility.Hidden;
        }

        private async Task UpdateVisibility(Window child, Visibility visibility, bool setFocus) {
            if (child == null) return;

            if (visibility != child.Visibility) {
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
            }
        }

        private void UpdateVisibility(bool keepFocus) {
            _busyUpdating.DoDelay(async () => {
                var visibility = GetVisibility();
                await UpdateVisibility(_child, visibility, keepFocus);
                foreach (var window in _attached) {
                    await UpdateVisibility(window, visibility, false);
                }
            }, 1);
        }

        protected void OnGotFocus(object sender, EventArgs e) {
            UpdateVisibility(true);
        }

        protected void OnLostFocus(object sender, EventArgs e) {
            UpdateVisibility(false);
        }

        public void Attach(string tag, Window window) {
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
            UpdateVisibility(true);
        }

        public bool IsActive => _child?.IsActive == true;
    }
}