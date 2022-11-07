using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls {
    public abstract class BaseSwitch : FrameworkElement {
        [CanBeNull]
        protected abstract UIElement GetChild();

        private readonly List<UIElement> _registeredElements = new List<UIElement>(2);
        private UIElement _child;
        private bool _busy;

        protected IReadOnlyList<UIElement> RegisteredElements => _registeredElements;

        protected int IndexOf(UIElement child) {
            return _registeredElements.IndexOf(child);
        }

        protected override IEnumerator LogicalChildren => ((IEnumerable)_registeredElements).GetEnumerator();

        protected override int VisualChildrenCount => _child == null ? 0 : 1;

        protected override Visual GetVisualChild(int index) {
            if (index != 0 || _child == null) throw new ArgumentOutOfRangeException();
            return _child;
        }

        protected void ClearRegisteredChildren() {
            foreach (var element in _registeredElements) {
                RemoveLogicalChild(element);
            }
            _registeredElements.Clear();
        }

        protected void RegisterChild(UIElement oldChild, UIElement newChild) {
            if (oldChild == newChild) return;
            Debug.Assert(oldChild == null || oldChild != _child);

            if (oldChild != null) {
                var i = _registeredElements.IndexOf(oldChild);
                if (i != -1) {
                    RemoveLogicalChild(oldChild);
                    _registeredElements.RemoveAt(i);
                }
            }

            if (newChild != null) {
                AddLogicalChild(newChild);
                _registeredElements.Add(newChild);
            }

            RefreshActiveChild();
        }

        private void SetActiveChild([CanBeNull] UIElement child) {
            if (ReferenceEquals(_child, child) || _busy) return;
            try {
                _busy = true;
                RemoveVisualChild(_child);
                AddVisualChild(child);
                _child = child;
            } finally {
                _busy = false;
            }
        }

        public void RefreshActiveChild() {
            if (GetChild() != _child) {
                InvalidateMeasure();
            }
        }

        protected static void OnChildRegisteringPropertyChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (!(sender is BaseSwitch b)) return;
            b.RegisterChild((UIElement)e.OldValue, (UIElement)e.NewValue);
            b.RefreshActiveChild();
        }

        protected static void OnChildSelectingPropertyChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (!(sender is BaseSwitch b)) return;
            b.RefreshActiveChild();
        }

        protected override Size MeasureOverride(Size constraint) {
            SetActiveChild(GetChild());

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