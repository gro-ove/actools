using System.Windows;
using System.Windows.Controls;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls {
    public abstract class BaseLazySwitch : Decorator {
        [CanBeNull]
        protected abstract UIElement GetChild();

        private UIElement _previousResource;
        private object _previousResourceKey;

        [CanBeNull]
        protected UIElement GetChildFromResources([CanBeNull] object key) {
            if (key != _previousResourceKey) {
                _previousResourceKey = key;
                _previousResource = key == null ? null : TryFindResource(key) as UIElement;
            }
            return _previousResource;
        }

        private UIElement _previousElement;
        private bool _previousElementReady;

        private UIElement GetChildInner() {
            if (!_previousElementReady) {
                _previousElement = GetChild();
                _previousElementReady = _previousElement != null || IsLoaded;
            }
            return _previousElement;
        }

        private UIElement _child;
        private bool _busy;

        private void SetActiveChild([CanBeNull] UIElement child) {
            if (ReferenceEquals(_child, child) || _busy) return;
            try {
                _busy = true;
                _child = child;
                Child = child;
            } finally {
                _busy = false;
            }
        }

        protected void UpdateActiveChild() {
            if (_child != GetChildInner()) {
                InvalidateMeasure();
            }
        }

        protected static void OnChildAffectingPropertyChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (!(sender is BaseLazySwitch b)) return;
            b._previousElementReady = false;
            b.UpdateActiveChild();
        }

        protected override Size MeasureOverride(Size constraint) {
            SetActiveChild(GetChildInner());
            var child = _child;
            if (child == null) return new Size();
            child.Measure(constraint);
            return child.DesiredSize;
        }
    }
}