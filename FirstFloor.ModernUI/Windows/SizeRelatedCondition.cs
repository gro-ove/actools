using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows {
    public static class SizeRelatedConditionExtension {
        /*public static SizeRelatedCondition AddSizeCondition<T, TParent>([NotNull] this TParent parent, [NotNull] Func<TParent, T> getChild,
                double widthThreshold, [NotNull] Action<T, Visibility> action) where TParent : FrameworkElement {
            return new SizeRelatedCondition<T, TParent>(parent, getChild, p => p.ActualWidth >= widthThreshold,
                    (t, b) => action(t, b ? Visibility.Visible : Visibility.Collapsed));
        }

        public static SizeRelatedCondition AddSizeCondition<T, TParent>([NotNull] this TParent parent, [NotNull] Func<TParent, T> getChild,
                [NotNull] Func<TParent, bool> condition, [NotNull] Action<T, Visibility> action) where TParent : FrameworkElement {
            return new SizeRelatedCondition<T, TParent>(parent, getChild, condition, (t, b) => action(t, b ? Visibility.Visible : Visibility.Collapsed));
        }

        public static SizeRelatedCondition AddSizeCondition<T, TParent>([NotNull] this TParent parent, [NotNull] Func<TParent, T> getChild,
                double widthThreshold) where TParent : FrameworkElement where T : FrameworkElement {
            return new SizeRelatedCondition<T, TParent>(parent, getChild, p => p.ActualWidth >= widthThreshold,
                    (t, b) => t.Visibility = b ? Visibility.Visible : Visibility.Collapsed);
        }

        public static SizeRelatedCondition AddSizeCondition<T, TParent>([NotNull] this TParent parent, [NotNull] Func<TParent, T> getChild,
                [NotNull] Func<TParent, bool> condition) where TParent : FrameworkElement where T : FrameworkElement {
            return new SizeRelatedCondition<T, TParent>(parent, getChild, condition,
                    (t, b) => t.Visibility = b ? Visibility.Visible : Visibility.Collapsed);
        }

        public static SizeRelatedCondition AddSizeCondition<T, TParent>([NotNull] this TParent parent, [NotNull] Func<TParent, T> getChild,
                double widthThreshold, [NotNull] Action<T, bool> action) where TParent : FrameworkElement {
            return new SizeRelatedCondition<T, TParent>(parent, getChild, p => p.ActualWidth >= widthThreshold, action);
        }

        public static SizeRelatedCondition AddSizeCondition<T, TParent>([NotNull] this TParent parent, [NotNull] Func<TParent, T> getChild,
                [NotNull] Func<TParent, bool> condition, [NotNull] Action<T, bool> action) where TParent : FrameworkElement {
            return new SizeRelatedCondition<T, TParent>(parent, getChild, condition, action);
        }

        public static SizeRelatedCondition AddSizeCondition<T, TParent>([NotNull] this TParent parent, T child,
                double widthThreshold, [NotNull] Action<T> moreAction, [NotNull] Action<T> lessAction) where TParent : FrameworkElement {
            return new SizeRelatedCondition<T, TParent>(parent, p => child, p => p.ActualWidth >= widthThreshold, (p, v) => {
                if (v) {
                    moreAction(p);
                } else {
                    lessAction(p);
                }
            });
        }

        public static SizeRelatedCondition AddSizeCondition<T, TParent>([NotNull] this TParent parent, T child,
                double widthThreshold, DependencyProperty property, object lessValue, object moreValue) where TParent : FrameworkElement
                where T : FrameworkElement {
            return new SizeRelatedCondition<T, TParent>(parent, p => child, p => p.ActualWidth >= widthThreshold,
                    (p, v) => p.SetValue(property, v ? moreValue : lessValue));
        }*/

        // Various conditions

        public static SizeRelatedCondition<TParent, bool> AddWidthCondition<TParent>([NotNull] this TParent parent, double widthThreshold)
                where TParent : FrameworkElement {
            return new SizeRelatedCondition<TParent, bool>(parent, p => p.ActualWidth >= widthThreshold);
        }

        public static SizeRelatedCondition<TParent, double> AddWidthCondition<TParent>([NotNull] this TParent parent, Func<double, double> condition)
                where TParent : FrameworkElement {
            return new SizeRelatedCondition<TParent, double>(parent, p => condition(p.ActualWidth));
        }

        public static SizeRelatedCondition<TParent, TValue> AddSizeCondition<TParent, TValue>([NotNull] this TParent parent, Func<TParent, TValue> condition)
                where TParent : FrameworkElement {
            return new SizeRelatedCondition<TParent, TValue>(parent, condition);
        }

        // Template-related (with getChild funs):

        public static SizeRelatedCondition<TParent, bool> Add<TParent>(this SizeRelatedCondition<TParent, bool> condition,
                Func<FrameworkElement> getChild) where TParent : FrameworkElement {
            return condition.Add(x => getChild(), (child, b) => child.Visibility = b ? Visibility.Visible : Visibility.Collapsed);
        }

        public static SizeRelatedCondition<TParent, bool> Add<TParent>(this SizeRelatedCondition<TParent, bool> condition,
                Func<DataGridColumn> getChild) where TParent : FrameworkElement {
            return condition.Add(x => getChild(), (child, b) => child.Visibility = b ? Visibility.Visible : Visibility.Collapsed);
        }

        public static SizeRelatedCondition<TParent, bool> Add<TParent>(this SizeRelatedCondition<TParent, bool> condition,
                Func<TParent, FrameworkElement> getChild) where TParent : FrameworkElement {
            return condition.Add(getChild, (child, b) => child.Visibility = b ? Visibility.Visible : Visibility.Collapsed);
        }

        public static SizeRelatedCondition<TParent, bool> Add<TParent>(this SizeRelatedCondition<TParent, bool> condition,
                Func<TParent, DataGridColumn> getChild) where TParent : FrameworkElement {
            return condition.Add(getChild, (child, b) => child.Visibility = b ? Visibility.Visible : Visibility.Collapsed);
        }

        // Already known chilren

        /*public static SizeRelatedCondition<TParent, bool> Add<TParent, TChild>(this SizeRelatedCondition<TParent, bool> condition,
                TChild child, Action<TChild> ) where TParent : FrameworkElement {
            return condition.Add(getChild, (child, b) => child.Visibility = b ? Visibility.Visible : Visibility.Collapsed);
        }*/

        public static SizeRelatedCondition<TParent, bool> AddInverted<TParent>(this SizeRelatedCondition<TParent, bool> condition,
                FrameworkElement child) where TParent : FrameworkElement {
            return condition.Add(b => child.Visibility = b ? Visibility.Collapsed : Visibility.Visible);
        }

        public static SizeRelatedCondition<TParent, bool> Add<TParent>(this SizeRelatedCondition<TParent, bool> condition,
                FrameworkElement child) where TParent : FrameworkElement {
            return condition.Add(b => child.Visibility = b ? Visibility.Visible : Visibility.Collapsed);
        }

        public static SizeRelatedCondition<TParent, bool> Add<TParent>(this SizeRelatedCondition<TParent, bool> condition,
                DataGridColumn child) where TParent : FrameworkElement {
            return condition.Add(b => child.Visibility = b ? Visibility.Visible : Visibility.Collapsed);
        }
    }

    public abstract class SizeRelatedCondition : IDisposable {
        public abstract void Update();

        private readonly Busy _updateLater = new Busy();
        public void UpdateLater() {
            _updateLater.DoDelay(Update, 1);
        }

        private static readonly Action EmptyDelegate = delegate {};

        public async void UpdateAfterRender() {
            await Application.Current.Dispatcher.InvokeAsync(EmptyDelegate, DispatcherPriority.Render).Task;
            Update();
        }

        public virtual void Dispose() { }
    }

    public class SizeRelatedCondition<TParent, TValue> : SizeRelatedCondition where TParent : FrameworkElement {
        private readonly TParent _parent;
        private readonly Func<TParent, TValue> _condition;

        internal SizeRelatedCondition([NotNull] TParent parent, [NotNull] Func<TParent, TValue> condition) {
            _parent = parent;
            _parent.SizeChanged += OnParentSizeChanged;
            _condition = condition;

            if (_parent.IsLoaded) {
                Update();
            } else {
                _parent.Loaded += OnParentLoaded;
            }
        }

        private void OnParentLoaded(object sender, RoutedEventArgs routedEventArgs) {
            _parent.Loaded -= OnParentLoaded;
            Update();
        }

        public override void Dispose() {
            base.Dispose();
            _parent.Loaded -= OnParentLoaded;
            _parent.SizeChanged -= OnParentSizeChanged;
        }

        private readonly List<ChildHolderBase> _children = new List<ChildHolderBase>(10);

        private abstract class ChildHolderBase {
            public abstract void Update(TParent parent);

            public abstract void Apply(TValue value);
        }

        private class ChildHolderEmpty : ChildHolderBase {
            private readonly Action<TValue> _applyFn;

            public ChildHolderEmpty(Action<TValue> applyFn) {
                _applyFn = applyFn;
            }

            public override void Update(TParent parent) {}

            public override void Apply(TValue value) {
                _applyFn(value);
            }
        }

        private class ChildHolder<TChild> : ChildHolderBase {
            private readonly Func<TParent, TChild> _getChild;
            private readonly Action<TChild, TValue> _applyFn;

            private TChild _child;
            private TValue _value;

            public ChildHolder(Func<TParent, TChild> getChild, Action<TChild, TValue> applyFn) {
                _getChild = getChild;
                _applyFn = applyFn;
            }

            public override void Update(TParent parent) {
                _child = _getChild(parent);
            }

            public override void Apply(TValue value) {
                _value = value;
                if (_child != null) {
                    _applyFn(_child, _value);
                }
            }
        }

        public SizeRelatedCondition<TParent, TValue> Add<TChild>(Func<TParent, TChild> getChild, Action<TChild, TValue> applyFn) {
            var h = new ChildHolder<TChild>(getChild, applyFn);

            if (_parent.IsLoaded) {
                if (!_stateSet) {
                    _stateSet = true;
                    _state = _condition(_parent);
                }

                h.Update(_parent);
                h.Apply(_state);
            }

            _children.Add(h);
            return this;
        }

        public SizeRelatedCondition<TParent, TValue> Add(Action<TValue> applyFn) {
            var h = new ChildHolderEmpty(applyFn);

            if (_parent.IsLoaded) {
                if (!_stateSet) {
                    _stateSet = true;
                    _state = _condition(_parent);
                }

                h.Update(_parent);
                h.Apply(_state);
            }

            _children.Add(h);
            return this;
        }

        private void OnParentSizeChanged(object sender, SizeChangedEventArgs sizeChangedEventArgs) {
            UpdateState();
        }

        private bool _stateSet;
        private TValue _state;

        public sealed override void Update() {
            _stateSet = true;
            _state = _condition(_parent);

            foreach (var h in _children) {
                h.Update(_parent);
                h.Apply(_state);
            }
        }

        private void UpdateState() {
            var state = _condition(_parent);
            if (!_stateSet || !Equals(_state, state)) {
                _stateSet = true;
                _state = state;

                foreach (var h in _children) {
                    h.Apply(_state);
                }
            }
        }
    }
}