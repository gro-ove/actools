using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using AcTools.Render.Wrapper;
using JetBrains.Annotations;
using SlimDX.Windows;

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
        private readonly int _offset;

        public AttachedHelper([NotNull] BaseFormWrapper parent, [NotNull] Window window, int offset = 10, int padding = 10) {
            _offset = offset;
            _padding = padding;

            _parent = parent.Form;
            _parent.Closed += OnClosed;
            _parent.UserResized += OnResize;
            _parent.Move += OnMove;
            _parent.Load += OnLoad;
            _parent.GotFocus += OnGotFocus;
            _parent.LostFocus += OnLostFocus;
            parent.FullscreenChanged += OnFullscreenChanged;

            _child = window;
            _child.Owner = null;
            ElementHost.EnableModelessKeyboardInterop(_child);

            _child.Show();
            _child.Activate();
            UpdatePosition();

            _child.Activated += ChildActivated;
            _child.Deactivated += ChildDeactivated;
            _child.Closed += ChildClosed;
            _child.KeyUp += ChildKeyUp;
            _child.LocationChanged += ChildLocationChanged;
            _child.SizeChanged += ChildSizeChanged;

            _child.Topmost = true;
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
                              where delta.Length < 20
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
            var location = GetStickyLocation(_stickyLocation, w, h);
            if (location == null || _child == null) return;

            _skip = true;
            _child.Left = location.Value.X;
            _child.Top = location.Value.Y;
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