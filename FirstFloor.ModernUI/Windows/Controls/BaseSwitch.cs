using System.Collections;
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
            get => GetValue(ResetElementNameBindingsProperty) as bool? == true;
            set => SetValue(ResetElementNameBindingsProperty, value);
        }

        [CanBeNull]
        protected abstract UIElement GetChild();

        private UIElement _child;
        private bool _busy;

        protected override IEnumerator LogicalChildren => _child == null ? EmptyEnumerator.Instance : new SingleChildEnumerator(_child);

        protected override int VisualChildrenCount => _child != null ? 1 : 0;

        protected override Visual GetVisualChild(int index) {
            return _child;
        }

        private bool _broken;

        private void ForcefullyDisconnect() {
            _broken = true;
            SetActiveChild(null);
        }

        private void SetActiveChild([CanBeNull] UIElement child) {
            if (ReferenceEquals(_child, child)) return;

            if (_busy) {
#if DEBUG
                Logging.Warning("Already updating");
#endif
                return;
            }

            _busy = true;

            if (_child != null) {
                RemoveVisualChild(_child);
                RemoveLogicalChild(_child);
            }

            _child = child;
            if (_child != null) {
                var parent = LogicalTreeHelper.GetParent(_child);
                if (parent is BaseSwitch otherSwitch) {
                    otherSwitch.ForcefullyDisconnect();
                } else if (parent != null) {
                    _broken = true;
                    _child = null;
                    Logging.Warning("Collision: " + parent);
                } else {
                    AddLogicalChild(_child);
                    AddVisualChild(_child);
                    if (ResetElementNameBindings) {
                        _child.ResetElementNameBindings();
                    }
                }
            }

            InvalidateMeasure();
            InvalidateVisual();
            _busy = false;
        }

        protected void UpdateActiveChild() {
            var newChild = _broken ? null : GetChild();
            if (newChild != _child) {
                InvalidateMeasure();
            }
        }

        protected override void OnRender(DrawingContext dc) {
            if (_broken) {
                dc.DrawRectangle(new SolidColorBrush(Colors.DarkRed), null, new Rect(new Point(), RenderSize));
            }
        }

        protected static void OnChildDefiningPropertyChanged(object sender, DependencyPropertyChangedEventArgs e) {
            if (!(sender is BaseSwitch b)) return;
            b.UpdateActiveChild();
        }

        protected override Size MeasureOverride(Size constraint) {
            SetActiveChild(_broken ? null : GetChild());
            if (_child == null) return new Size();
            _child.Measure(constraint);
            return _child.DesiredSize;
        }

        protected override Size ArrangeOverride(Size arrangeBounds) {
            _child?.Arrange(new Rect(arrangeBounds));
            return arrangeBounds;
        }

        protected static void OnWhenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            if (d is UIElement element) {
                element.GetParent<BaseSwitch>()?.UpdateActiveChild();
            }
        }
    }
}