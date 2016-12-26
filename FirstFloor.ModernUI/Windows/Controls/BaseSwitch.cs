using System;
using System.Collections;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Media;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls {
    public abstract class BaseSwitch : FrameworkElement {
        public static readonly DependencyProperty ResetElementNameBindingsProperty = DependencyProperty.Register(nameof(ResetElementNameBindings), typeof(bool),
                typeof(BaseSwitch), new FrameworkPropertyMetadata(false));

        public bool ResetElementNameBindings {
            get { return (bool)GetValue(ResetElementNameBindingsProperty); }
            set { SetValue(ResetElementNameBindingsProperty, value); }
        }

        [CanBeNull]
        protected abstract UIElement GetChild();


        private UIElement _child;

        private void SetActiveChild(UIElement child) {
            if (ReferenceEquals(_child, child)) return;

            RemoveVisualChild(_child);
            RemoveLogicalChild(_child);

            _child = child;

            AddLogicalChild(_child);
            AddVisualChild(_child);

            if (ResetElementNameBindings) {
                var w = Stopwatch.StartNew();
                child.ResetElementNameBindings();
                Logging.Debug($"{w.Elapsed.TotalMilliseconds:F2} ms");
            }
        }

        protected override IEnumerator LogicalChildren => _child == null ? EmptyEnumerator.Instance :
                new SingleChildEnumerator(_child);

        protected override Size MeasureOverride(Size constraint) {
            SetActiveChild(GetChild());

            var e = _child;
            if (e == null) return Size.Empty;

            e.Measure(constraint);
            return e.DesiredSize;
        }

        protected override Size ArrangeOverride(Size arrangeBounds) {
            _child?.Arrange(new Rect(arrangeBounds));
            return arrangeBounds;
        }

        protected override int VisualChildrenCount => _child != null ? 1 : 0;

        protected override Visual GetVisualChild(int index) {
            var child = _child;
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

    internal class EmptyEnumerator : IEnumerator {
        private EmptyEnumerator() { }

        public static IEnumerator Instance => _instance ?? (_instance = new EmptyEnumerator());

        public void Reset() { }

        public bool MoveNext() { return false; }

        public object Current {
            get { throw new InvalidOperationException(); }
        }

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