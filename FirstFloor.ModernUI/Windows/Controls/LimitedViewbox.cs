using System;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class LimitedViewbox : Decorator {
        private ContainerVisual _internalVisual;

        static LimitedViewbox() { }

        private ContainerVisual InternalVisual {
            get {
                if (_internalVisual == null) {
                    _internalVisual = new ContainerVisual();
                    AddVisualChild(_internalVisual);
                }
                return _internalVisual;
            }
        }

        private UIElement InternalChild {
            get {
                var children = InternalVisual.Children;
                return children.Count != 0 ? children[0] as UIElement : null;
            }
            set {
                var children = InternalVisual.Children;
                if (children.Count != 0) {
                    children.Clear();
                }
                children.Add(value);
            }
        }

        private Transform InternalTransform {
            get => InternalVisual.Transform;
            set => InternalVisual.Transform = value;
        }

        public override UIElement Child {
            get => InternalChild;
            set {
                var internalChild = InternalChild;
                if (ReferenceEquals(internalChild, value)) return;

                RemoveLogicalChild(internalChild);
                if (value != null) {
                    AddLogicalChild(value);
                }

                InternalChild = value;
                InvalidateMeasure();
            }
        }

        protected override int VisualChildrenCount => 1;

        protected override Visual GetVisualChild(int index) {
            if (index != 0) throw new ArgumentOutOfRangeException(nameof(index));
            return InternalVisual;
        }

        protected override IEnumerator LogicalChildren => InternalChild == null ? EmptyEnumerator.Instance :
                new SingleChildEnumerator(InternalChild);

        public static readonly DependencyProperty StretchProperty = DependencyProperty.Register(nameof(Stretch), typeof(Stretch),
                typeof(LimitedViewbox), new FrameworkPropertyMetadata(Stretch.Uniform, FrameworkPropertyMetadataOptions.AffectsMeasure, (o, e) => {
                    ((LimitedViewbox)o)._stretch = (Stretch)e.NewValue;
                }));

        private Stretch _stretch = Stretch.Uniform;

        public Stretch Stretch {
            get => _stretch;
            set => SetValue(StretchProperty, value);
        }

        public static readonly DependencyProperty StretchDirectionProperty = DependencyProperty.Register(nameof(StretchDirection), typeof(StretchDirection),
                typeof(LimitedViewbox), new FrameworkPropertyMetadata(StretchDirection.Both, FrameworkPropertyMetadataOptions.AffectsMeasure, (o, e) => {
                    ((LimitedViewbox)o)._stretchDirection = (StretchDirection)e.NewValue;
                }));

        private StretchDirection _stretchDirection = StretchDirection.Both;

        public StretchDirection StretchDirection {
            get => _stretchDirection;
            set => SetValue(StretchDirectionProperty, value);
        }

        public static readonly DependencyProperty MinimumScaleProperty = DependencyProperty.Register(nameof(MinimumScale), typeof(double),
                typeof(LimitedViewbox), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsMeasure, (o, e) => {
                    ((LimitedViewbox)o)._minimumScale = (double)e.NewValue;
                }));

        private double _minimumScale = 0d;

        public double MinimumScale {
            get => _minimumScale;
            set => SetValue(MinimumScaleProperty, value);
        }

        public static readonly DependencyProperty MaximumScaleProperty = DependencyProperty.Register(nameof(MaximumScale), typeof(double),
                typeof(LimitedViewbox), new FrameworkPropertyMetadata(double.PositiveInfinity, FrameworkPropertyMetadataOptions.AffectsMeasure, (o, e) => {
                    ((LimitedViewbox)o)._maximumScale = (double)e.NewValue;
                }));

        private double _maximumScale = double.PositiveInfinity;

        public double MaximumScale {
            get => _maximumScale;
            set => SetValue(MaximumScaleProperty, value);
        }

        protected override Size MeasureOverride(Size constraint) {
            var internalChild = InternalChild;
            var size = new Size();
            if (internalChild != null) {
                var availableSize = new Size(double.PositiveInfinity, double.PositiveInfinity);

                if (MinimumScale != 0d) {
                    availableSize.Width = constraint.Width / MinimumScale;
                    availableSize.Height = constraint.Height / MinimumScale;
                }

                internalChild.Measure(availableSize);
                var desiredSize = internalChild.DesiredSize;
                var scaleFactor = ComputeScaleFactor(constraint, desiredSize, Stretch, StretchDirection);
                size.Width = scaleFactor.Width * desiredSize.Width;
                size.Height = scaleFactor.Height * desiredSize.Height;
            }
            return size;
        }

        protected override Size ArrangeOverride(Size arrangeSize) {
            var internalChild = InternalChild;
            if (internalChild != null) {
                var desiredSize = internalChild.DesiredSize;
                var scaleFactor = ComputeScaleFactor(arrangeSize, desiredSize, Stretch, StretchDirection);
                InternalTransform = new ScaleTransform(scaleFactor.Width, scaleFactor.Height);
                internalChild.Arrange(new Rect(new Point(), desiredSize));
                arrangeSize.Width = scaleFactor.Width * desiredSize.Width;
                arrangeSize.Height = scaleFactor.Height * desiredSize.Height;
            }
            return arrangeSize;
        }

        private Size ComputeScaleFactor(Size availableSize, Size contentSize, Stretch stretch, StretchDirection stretchDirection) {
            var width = 1d;
            var height = 1d;
            var flag1 = !double.IsPositiveInfinity(availableSize.Width);
            var flag2 = !double.IsPositiveInfinity(availableSize.Height);

            var minumum = stretchDirection == StretchDirection.UpOnly ? Math.Max(1d, MinimumScale) : MinimumScale;
            var maximum = stretchDirection == StretchDirection.DownOnly ? Math.Min(1d, MaximumScale) : MaximumScale;

            if ((stretch == Stretch.Uniform || stretch == Stretch.UniformToFill || stretch == Stretch.Fill) && flag1 | flag2) {
                width = contentSize.Width == 0d ? 0d : availableSize.Width / contentSize.Width;
                height = contentSize.Height == 0d ? 0d : availableSize.Height / contentSize.Height;
                if (!flag1) {
                    width = height;
                } else if (!flag2) {
                    height = width;
                } else {
                    switch (stretch) {
                        case Stretch.Uniform:
                            var num1 = width < height ? width : height;
                            width = height = num1;
                            break;
                        case Stretch.UniformToFill:
                            var num2 = width > height ? width : height;
                            width = height = num2;
                            break;
                    }
                }

                if (width < minumum) width = minumum;
                if (height < minumum) height = minumum;
                if (width > maximum) width = maximum;
                if (height > maximum) height = maximum;
            }

            return new Size(width, height);
        }
    }
}