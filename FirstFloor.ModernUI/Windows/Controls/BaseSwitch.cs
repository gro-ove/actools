using System;
using System.Windows;
using System.Windows.Media;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls {
    public abstract class BaseSwitch : FrameworkElement {
        [CanBeNull]
        protected abstract UIElement GetChild();


        private UIElement _activeChild;

        private void SetActiveChild(UIElement element) {
            if (ReferenceEquals(_activeChild, element)) return;

            if (_activeChild != null) {
                RemoveLogicalChild(_activeChild);
                RemoveVisualChild(_activeChild);
            }

            _activeChild = element;

            if (_activeChild != null) {
                AddLogicalChild(_activeChild);
                AddVisualChild(_activeChild);
            }
        }

        protected override Size MeasureOverride(Size constraint) {
            SetActiveChild(GetChild());

            var e = _activeChild;
            if (e == null) return Size.Empty;

            e.Measure(constraint);
            return e.DesiredSize;
        }

        protected override Size ArrangeOverride(Size arrangeBounds) {
            _activeChild?.Arrange(new Rect(arrangeBounds));
            return arrangeBounds;
        }

        protected override int VisualChildrenCount => _activeChild != null ? 1 : 0;

        protected override Visual GetVisualChild(int index) {
            var child = _activeChild;
            if (child == null || index != 0) throw new ArgumentOutOfRangeException(nameof(index));
            return child;
        }
        
        protected static void OnWhenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var element = d as UIElement;
            if (element != null) {
                (VisualTreeHelper.GetParent(element) as BaseSwitch)?.InvalidateMeasure();
            }
        }
    }
}