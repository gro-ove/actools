using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls {
    public abstract class BaseSwitch : FrameworkElement {
        private readonly UIElementCollection _uiElementCollection;

        [CanBeNull]
        protected abstract UIElement GetChild();

        private UIElement _child;
        private bool _busy;

        protected override IEnumerator LogicalChildren => _uiElementCollection.GetEnumerator();

        protected override int VisualChildrenCount => _uiElementCollection.Count;

        protected override Visual GetVisualChild(int index) {
            return _uiElementCollection[index];
        }

        protected BaseSwitch() {
            _uiElementCollection = new UIElementCollection(this, this);
        }

        protected void ClearRegisteredChildren() {
            _uiElementCollection.Clear();
        }

        protected void RegisterChild(UIElement oldChild, UIElement newChild) {
            if (oldChild == newChild) return;
            if (oldChild != null) _uiElementCollection.Remove(oldChild);
            if (newChild != null) _uiElementCollection.Add(newChild);
        }

        private void SetActiveChild([CanBeNull] UIElement child) {
            if (ReferenceEquals(_child, child) || _busy) return;
            try {
                _busy = true;
                _child = child;
                foreach (UIElement element in _uiElementCollection) {
                    element.Visibility = element == child ? Visibility.Visible : Visibility.Collapsed;
                }
            } finally {
                _busy = false;
            }
        }

        private void UpdateActiveChild() {
            SetActiveChild(GetChild());
        }

        protected static void OnChildSettingPropertyChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (!(sender is BaseSwitch b)) return;
            b.RegisterChild((UIElement)e.OldValue, (UIElement)e.NewValue);
            b.UpdateActiveChild();
        }

        protected static void OnChildDefiningPropertyChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (!(sender is BaseSwitch b)) return;
            b.UpdateActiveChild();
        }

        public override void OnApplyTemplate() {
            base.OnApplyTemplate();
            UpdateActiveChild();
        }

        protected static void OnWhenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is UIElement element) {
                element.GetParent<BaseSwitch>()?.UpdateActiveChild();
            }
        }

        protected override Size MeasureOverride(Size constraint) {
            var child = _child;
            if (child == null) return new Size();
            child.Measure(constraint);
            return child.DesiredSize;
        }

        protected override Size ArrangeOverride(Size arrangeBounds) {
            _child?.Arrange(new Rect(arrangeBounds));
            return arrangeBounds;
        }
    }
}