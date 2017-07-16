using System.Reflection;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class ThumbExt : Thumb {
        [CanBeNull]
        private static readonly DependencyPropertyKey IsDraggingPropertyKey;

        static ThumbExt() {
            IsDraggingPropertyKey = typeof(Thumb).GetField("IsDraggingPropertyKey", BindingFlags.NonPublic | BindingFlags.Static)?
                                                 .GetValue(null) as DependencyPropertyKey;
        }

        protected override void OnLostMouseCapture(MouseEventArgs e) {
            var thumb = this;
            if (ReferenceEquals(Mouse.Captured, thumb)) return;
            thumb.CancelDragOverride();
        }

        private void CancelDragOverride() {
            if (IsDraggingPropertyKey == null) {
                CancelDrag();
                return;
            }

            if (!IsDragging) return;
            if (IsMouseCaptured) ReleaseMouseCapture();
            ClearValue(IsDraggingPropertyKey);

            var multiplier = Keyboard.Modifiers == ModifierKeys.Shift ? 0.2 : 1d;
            RaiseEvent(new DragCompletedEventArgs(
                    (_previousScreenCoordPosition.X - _originScreenCoordPosition.X) * multiplier,
                    (_previousScreenCoordPosition.Y - _originScreenCoordPosition.Y) * multiplier, true));
        }

        private Point _originThumbPoint;
        private Point _originScreenCoordPosition;
        private Point _previousScreenCoordPosition;

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) {
            if (IsDraggingPropertyKey == null) {
                base.OnMouseLeftButtonDown(e);
                return;
            }

            if (!IsDragging) {
                e.Handled = true;
                Focus();
                CaptureMouse();
                SetValue(IsDraggingPropertyKey, true);
                _originThumbPoint = e.GetPosition(this);
                _previousScreenCoordPosition = _originScreenCoordPosition = PointToScreen(_originThumbPoint);
                var flag = true;
                try {
                    RaiseEvent(new DragStartedEventArgs(_originThumbPoint.X, _originThumbPoint.Y));
                    flag = false;
                } finally {
                    if (flag) CancelDragOverride();
                }
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) {
            if (IsDraggingPropertyKey == null) {
                base.OnMouseLeftButtonUp(e);
                return;
            }

            if (IsMouseCaptured && IsDragging) {
                e.Handled = true;
                ClearValue(IsDraggingPropertyKey);
                ReleaseMouseCapture();
                var screen = PointToScreen(e.MouseDevice.GetPosition(this));

                var multiplier = Keyboard.Modifiers == ModifierKeys.Shift ? 0.2 : 1d;
                RaiseEvent(new DragCompletedEventArgs(
                        (screen.X - _originScreenCoordPosition.X) * multiplier,
                        (screen.Y - _originScreenCoordPosition.Y) * multiplier, false));
            }
        }

        protected override void OnMouseMove(MouseEventArgs e) {
            if (IsDraggingPropertyKey == null) {
                base.OnMouseMove(e);
                return;
            }

            if (!IsDragging) return;
            if (e.MouseDevice.LeftButton == MouseButtonState.Pressed) {
                var position = e.GetPosition(this);
                var screen = PointToScreen(position);
                if (!(screen != _previousScreenCoordPosition)) return;
                _previousScreenCoordPosition = screen;
                e.Handled = true;

                var multiplier = Keyboard.Modifiers == ModifierKeys.Shift ? 0.2 : 1d;
                RaiseEvent(new DragDeltaEventArgs((position.X - _originThumbPoint.X) * multiplier, (position.Y - _originThumbPoint.Y) * multiplier));
                if (multiplier != 1d) {
                    _originThumbPoint.X = position.X;
                    _originThumbPoint.Y = position.Y;
                }
            } else {
                if (ReferenceEquals(e.MouseDevice.Captured, this)) ReleaseMouseCapture();
                ClearValue(IsDraggingPropertyKey);
                _originThumbPoint.X = 0.0;
                _originThumbPoint.Y = 0.0;
            }
        }
    }
}