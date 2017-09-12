using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls {
    public abstract class BaseSwitch : Control {
        static BaseSwitch() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BaseSwitch), new FrameworkPropertyMetadata(typeof(BaseSwitch)));
        }

        public static readonly DependencyProperty ResetElementNameBindingsProperty = DependencyProperty.Register(nameof(ResetElementNameBindings), typeof(bool),
                typeof(BaseSwitch), new FrameworkPropertyMetadata(false));

        public bool ResetElementNameBindings {
            get => GetValue(ResetElementNameBindingsProperty) as bool? == true;
            set => SetValue(ResetElementNameBindingsProperty, value);
        }

        public static readonly DependencyPropertyKey ContentPropertyKey = DependencyProperty.RegisterReadOnly(nameof(Content), typeof(object),
                typeof(BaseSwitch), new PropertyMetadata(null));

        public static readonly DependencyProperty ContentProperty = ContentPropertyKey.DependencyProperty;

        public object Content => GetValue(ContentProperty);

        [CanBeNull]
        protected abstract UIElement GetChild();

        private UIElement _child;

        private void SetActiveChild(UIElement child) {
            if (ReferenceEquals(_child, child)) return;

            _child = child;
            SetValue(ContentPropertyKey, child);

            if (ResetElementNameBindings) {
                child.ResetElementNameBindings();
            }
        }

        protected void UpdateActiveChild() {
            SetActiveChild(GetChild());
        }

        protected static void OnChildDefiningPropertyChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (!(sender is BaseSwitch b)) return;
            b.UpdateActiveChild();
            b.InvalidateMeasure();
            b.InvalidateVisual();
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
                (VisualTreeHelper.GetParent(element) as BaseSwitch)?.UpdateActiveChild();
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