using System;
using System.Collections;
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

        private void SetActiveChild([CanBeNull] UIElement child) {
            if (ReferenceEquals(_child, child)) return;

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
        }

        private void UpdateActiveChild() {
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

    internal class EmptyEnumerator : IEnumerator {
        private EmptyEnumerator() { }

        public static IEnumerator Instance => _instance ?? (_instance = new EmptyEnumerator());

        public void Reset() { }

        public bool MoveNext() { return false; }

        public object Current => throw new InvalidOperationException();

        private static IEnumerator _instance;
    }

    internal class SingleChildEnumerator : IEnumerator {
        internal SingleChildEnumerator(object child) {
            _child = child;
            _count = child == null ? 0 : 1;
        }

        object IEnumerator.Current => _index == 0 ? _child : null;

        bool IEnumerator.MoveNext() {
            return ++_index < _count;
        }

        void IEnumerator.Reset() {
            _index = -1;
        }

        private int _index = -1;
        private readonly int _count;
        private readonly object _child;
    }
}