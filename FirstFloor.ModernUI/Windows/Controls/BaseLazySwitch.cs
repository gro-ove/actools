using System.Windows;
using System.Windows.Controls;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls {
    public abstract class BaseLazySwitch : Decorator {
        [CanBeNull]
        protected abstract UIElement GetChild();

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
            SetActiveChild(GetChild());
        }

        protected static void OnChildDefiningPropertyChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (!(sender is BaseLazySwitch b)) return;
            b.UpdateActiveChild();
        }

        public override void OnApplyTemplate() {
            base.OnApplyTemplate();
            UpdateActiveChild();
        }
    }
}