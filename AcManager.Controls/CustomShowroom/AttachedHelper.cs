using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using AcTools.Render.Wrapper;
using JetBrains.Annotations;
using SlimDX.Windows;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace AcManager.Controls.CustomShowroom {
    internal class AttachedHelper {
        private readonly RenderForm _parent;

        [CanBeNull]
        private Window _child;

        private bool _visible;

        public bool Visible {
            get { return _visible; }
            set {
                if (Equals(_visible, value)) return;
                _visible = value;
                UpdateVisibility(true);
            }
        }
        
        private readonly int _padding;
        private readonly bool _limitHeight;
        private readonly int _offset;

        public AttachedHelper([NotNull] BaseFormWrapper parent, [NotNull] Window window, int offset = -1, int padding = 10, bool limitHeight = true) {
            _padding = padding;
            _limitHeight = limitHeight;

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
            child.KeyUp += ChildKeyUp;
            child.LocationChanged += ChildLocationChanged;
            child.SizeChanged += ChildSizeChanged;

            child.Topmost = true;
            Visible = true;
        }

        private void OnClosed(object sender, EventArgs e) {
            if (_child != null) {
                _child.Close();
                _child = null;
            }
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

        private void ChildKeyUp(object sender, KeyEventArgs e) {
            if (e.Key == Key.H && Keyboard.Modifiers == ModifierKeys.Control) {
                _visible = !_visible;
                UpdateVisibility(true);
                e.Handled = true;
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
        }

        private bool _updating;

        private async void UpdateVisibility(bool keepFocus) {
            if (_updating) return;

            _updating = true;
            await Task.Delay(1);

            if (_child != null) {
                var val = _visible && (_parent.Focused || _child.IsActive) ? Visibility.Visible : Visibility.Hidden;
                if (val != _child.Visibility) {
                    _child.Visibility = val;

                    if (val == Visibility.Visible) {
                        _child.Topmost = false;
                        _child.Topmost = true;
                    }

                    if (keepFocus) {
                        await Task.Delay(1);
                        _parent.Focus();
                        _parent.Activate();
                    }
                }
            }

            _updating = false;
        }

        protected void OnGotFocus(object sender, EventArgs e) {
            UpdateVisibility(true);
        }

        protected void OnLostFocus(object sender, EventArgs e) {
            UpdateVisibility(false);
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