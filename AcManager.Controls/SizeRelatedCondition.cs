
using System;
using System.Windows;
using JetBrains.Annotations;

namespace AcManager.Controls {
    public static class SizeRelatedConditionExtension {
        public static SizeRelatedCondition AddSizeCondition<T, TParent>([NotNull] this TParent parent, [NotNull] Func<TParent, T> getChild,
                float widthThreshold, [NotNull] Action<T, Visibility> action) where TParent : FrameworkElement {
            return new SizeRelatedCondition<T, TParent>(parent, getChild, p => p.ActualWidth >= widthThreshold,
                    (t, b) => action(t, b ? Visibility.Visible : Visibility.Collapsed));
        }

        public static SizeRelatedCondition AddSizeCondition<T, TParent>([NotNull] this TParent parent, [NotNull] Func<TParent, T> getChild,
                [NotNull] Func<TParent, bool> condition, [NotNull] Action<T, Visibility> action) where TParent : FrameworkElement {
            return new SizeRelatedCondition<T, TParent>(parent, getChild, condition, (t, b) => action(t, b ? Visibility.Visible : Visibility.Collapsed));
        }

        public static SizeRelatedCondition AddSizeCondition<T, TParent>([NotNull] this TParent parent, [NotNull] Func<TParent, T> getChild,
                float widthThreshold) where TParent : FrameworkElement where T : FrameworkElement {
            return new SizeRelatedCondition<T, TParent>(parent, getChild, p => p.ActualWidth >= widthThreshold,
                    (t, b) => t.Visibility = b ? Visibility.Visible : Visibility.Collapsed);
        }

        public static SizeRelatedCondition AddSizeCondition<T, TParent>([NotNull] this TParent parent, [NotNull] Func<TParent, T> getChild,
                [NotNull] Func<TParent, bool> condition) where TParent : FrameworkElement where T : FrameworkElement {
            return new SizeRelatedCondition<T, TParent>(parent, getChild, condition,
                    (t, b) => t.Visibility = b ? Visibility.Visible : Visibility.Collapsed);
        }

        public static SizeRelatedCondition AddSizeCondition<T, TParent>([NotNull] this TParent parent, [NotNull] Func<TParent, T> getChild,
                float widthThreshold, [NotNull] Action<T, bool> action) where TParent : FrameworkElement {
            return new SizeRelatedCondition<T, TParent>(parent, getChild, p => p.ActualWidth >= widthThreshold, action);
        }

        public static SizeRelatedCondition AddSizeCondition<T, TParent>([NotNull] this TParent parent, [NotNull] Func<TParent, T> getChild,
                [NotNull] Func<TParent, bool> condition, [NotNull] Action<T, bool> action) where TParent : FrameworkElement {
            return new SizeRelatedCondition<T, TParent>(parent, getChild, condition, action);
        }

        public static SizeRelatedCondition AddSizeCondition<T, TParent>([NotNull] this TParent parent, T child,
                float widthThreshold, [NotNull] Action<T> moreAction, [NotNull] Action<T> lessAction) where TParent : FrameworkElement {
            return new SizeRelatedCondition<T, TParent>(parent, p => child, p => p.ActualWidth >= widthThreshold, (p, v) => {
                if (v) {
                    moreAction(p);
                } else {
                    lessAction(p);
                }
            });
        }

        public static SizeRelatedCondition AddSizeCondition<T, TParent>([NotNull] this TParent parent, T child,
                float widthThreshold, DependencyProperty property, object lessValue, object moreValue) where TParent : FrameworkElement
                where T : FrameworkElement {
            return new SizeRelatedCondition<T, TParent>(parent, p => child, p => p.ActualWidth >= widthThreshold,
                    (p, v) => p.SetValue(property, v ? moreValue : lessValue));
        }
    }

    public abstract class SizeRelatedCondition {
        public abstract void Update();
    }

    public class SizeRelatedCondition<T, TParent> : SizeRelatedCondition where TParent : FrameworkElement {
        private readonly TParent _parent;
        private readonly Func<TParent, T> _getChild;
        private readonly Func<TParent, bool> _condition;
        private readonly Action<T, bool> _action;

        private T _element;

        [CanBeNull]
        private T Element {
            get { return _element; }
            set {
                if (Equals(value, _element)) return;

                if (value == null != (_element == null)) {
                    if (value == null) {
                        _parent.SizeChanged -= OnParentSizeChanged;
                    } else {
                        _parent.SizeChanged += OnParentSizeChanged;
                    }
                }

                _element = value;
                _state = null;
                UpdateState();
            }
        }

        public sealed override void Update() {
            Element = _getChild(_parent);
        }

        internal SizeRelatedCondition([NotNull] TParent parent, [NotNull] Func<TParent, T> getChild,
                [NotNull] Func<TParent, bool> condition, [NotNull] Action<T, bool> action) {
            _parent = parent;
            _getChild = getChild;
            _condition = condition;
            _action = action;

            if (_parent.IsInitialized) {
                Update();
            }
        }

        private void OnParentSizeChanged(object sender, SizeChangedEventArgs sizeChangedEventArgs) {
            UpdateState();
        }

        private bool? _state;

        private void UpdateState() {
            if (Element == null) return;

            var state = _condition(_parent);
            if (_state != state) {
                _action(Element, state);
            }
        }
    }
}