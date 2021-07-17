using System.Windows;
using System.Windows.Controls;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls {
    public abstract class BaseSwitch : ContentControl {
        public static readonly DependencyProperty ResetElementNameBindingsProperty = DependencyProperty.Register(nameof(ResetElementNameBindings), typeof(bool),
                typeof(BaseSwitch), new FrameworkPropertyMetadata(false));

        public bool ResetElementNameBindings {
            get => GetValue(ResetElementNameBindingsProperty) as bool? == true;
            set => SetValue(ResetElementNameBindingsProperty, value);
        }

        [CanBeNull]
        protected abstract UIElement GetChild();

        private UIElement _child;
        private bool _reattachChild;
        private bool _busy;

        private void SetActiveChild([CanBeNull] UIElement child) {
            if (ReferenceEquals(_child, child) || _busy) return;
            try {
                _busy = true;
                if (_reattachChild) {
                    Content = null;
                    AddLogicalChild(_child);
                }
                _reattachChild = child is FrameworkElement fe && fe.Parent == this;
                if (_reattachChild) {
                    RemoveLogicalChild(child);
                }
                _child = child;
                Content = child;
                if (ResetElementNameBindings) {
                    child?.ResetElementNameBindings();
                }
                InvalidateMeasure();
                InvalidateVisual();
            } finally {
                _busy = false;
            }
        }

        protected void UpdateActiveChild() {
            SetActiveChild(GetChild());
        }

        protected static void OnChildDefiningPropertyChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (!(sender is BaseSwitch b)) return;
            // b.InvalidateMeasure();
            b.UpdateActiveChild();
        }

        public override void OnApplyTemplate() {
            base.OnApplyTemplate();
            UpdateActiveChild();
        }

        protected override Size MeasureOverride(Size constraint) {
            UpdateActiveChild();
            return base.MeasureOverride(constraint);
        }

        protected static void OnWhenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is UIElement element) {
                element.GetParent<BaseSwitch>()?.UpdateActiveChild();
            }
        }
    }
}