using System;
using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using FirstFloor.ModernUI.Serialization;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class BorderyViewbox : Decorator {
        public static readonly DependencyProperty StretchProperty = DependencyProperty.Register(nameof(Stretch),
                typeof(Stretch), typeof(BorderyViewbox), new FrameworkPropertyMetadata(Stretch.Uniform, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public Stretch Stretch {
            get => GetValue(StretchProperty) as Stretch? ?? default;
            set => SetValue(StretchProperty, value);
        }

        public static readonly DependencyProperty StretchDirectionProperty = DependencyProperty.Register(nameof(StretchDirection),
                typeof(StretchDirection), typeof(BorderyViewbox), new FrameworkPropertyMetadata(StretchDirection.Both, FrameworkPropertyMetadataOptions.AffectsMeasure));

        public StretchDirection StretchDirection {
            get => GetValue(StretchDirectionProperty) as StretchDirection? ?? default;
            set => SetValue(StretchDirectionProperty, value);
        }

        public static readonly DependencyPropertyKey ScalePropertyKey = DependencyProperty.RegisterReadOnly(nameof(Scale), typeof(Size),
                typeof(BorderyViewbox), new PropertyMetadata(default(Size)));

        public static readonly DependencyProperty ScaleProperty = ScalePropertyKey.DependencyProperty;

        public Size Scale => GetValue(ScaleProperty) as Size? ?? default;

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
                var vc = InternalVisual.Children;
                return vc.Count != 0 ? vc[0] as UIElement : null;
            }
            set {
                var vc = InternalVisual.Children;
                if (vc.Count != 0) vc.Clear();
                vc.Add(value);
            }
        }

        private Transform InternalTransform {
            get => InternalVisual.Transform;
            set => InternalVisual.Transform = value;
        }

        public override UIElement Child {
            get => InternalChild;
            set {
                var old = InternalChild;
                if (!ReferenceEquals(old, value)) {
                    RemoveLogicalChild(old);

                    if (value != null) {
                        AddLogicalChild(value);
                    }

                    InternalChild = value;
                    InvalidateMeasure();
                }
            }
        }

        protected override int VisualChildrenCount => 1;

        protected override Visual GetVisualChild(int index) {
            if (index != 0) throw new ArgumentOutOfRangeException(nameof(index));
            return InternalVisual;
        }

        protected override IEnumerator LogicalChildren => InternalChild == null ? EmptyEnumerator.Instance : new SingleChildEnumerator(InternalChild);

        protected override Size MeasureOverride(Size constraint) {
            var child = InternalChild;
            var parentSize = new Size();

            if (child != null) {
                var infinteConstraint = new Size(double.PositiveInfinity, double.PositiveInfinity);

                child.Measure(infinteConstraint);
                var childSize = child.DesiredSize;

                var scale = ComputeScaleFactor(constraint, childSize, Stretch, StretchDirection);
                SetValue(ScalePropertyKey, scale);

                parentSize.Width = scale.Width * childSize.Width;
                parentSize.Height = scale.Height * childSize.Height;
            }

            return parentSize;

        }

        protected override Size ArrangeOverride(Size arrangeSize) {
            var child = InternalChild;
            if (child != null) {
                var childSize = child.DesiredSize;

                var scale = ComputeScaleFactor(arrangeSize, childSize, Stretch, StretchDirection);
                SetValue(ScalePropertyKey, scale);

                InternalTransform = new ScaleTransform(scale.Width, scale.Height);
                child.Arrange(new Rect(new Point(), child.DesiredSize));

                arrangeSize.Width = scale.Width * childSize.Width;
                arrangeSize.Height = scale.Height * childSize.Height;
            }
            return arrangeSize;
        }

        private static Size ComputeScaleFactor(Size availableSize, Size contentSize, Stretch stretch, StretchDirection stretchDirection) {
            var scaleX = 1.0;
            var scaleY = 1.0;

            var isConstrainedWidth = !double.IsPositiveInfinity(availableSize.Width);
            var isConstrainedHeight = !double.IsPositiveInfinity(availableSize.Height);

            if ((stretch == Stretch.Uniform || stretch == Stretch.UniformToFill || stretch == Stretch.Fill)
                    && (isConstrainedWidth || isConstrainedHeight)) {
                scaleX = Equals(0d, contentSize.Width) ? 0.0 : availableSize.Width / contentSize.Width;
                scaleY = Equals(0d, contentSize.Height) ? 0.0 : availableSize.Height / contentSize.Height;

                if (!isConstrainedWidth) {
                    scaleX = scaleY;
                } else if (!isConstrainedHeight) {
                    scaleY = scaleX;
                } else {
                    switch (stretch) {
                        case Stretch.Uniform:
                            var minscale = scaleX < scaleY ? scaleX : scaleY;
                            scaleX = scaleY = minscale;
                            break;

                        case Stretch.UniformToFill:
                            var maxscale = scaleX > scaleY ? scaleX : scaleY;
                            scaleX = scaleY = maxscale;
                            break;

                        case Stretch.Fill:
                            break;
                    }
                }

                switch (stretchDirection) {
                    case StretchDirection.UpOnly:
                        if (scaleX < 1.0) scaleX = 1.0;
                        if (scaleY < 1.0) scaleY = 1.0;
                        break;

                    case StretchDirection.DownOnly:
                        if (scaleX > 1.0) scaleX = 1.0;
                        if (scaleY > 1.0) scaleY = 1.0;
                        break;

                    case StretchDirection.Both:
                        break;
                }
            }

            return new Size(scaleX, scaleY);
        }

        private ContainerVisual _internalVisual;

        #region Converter-related stuff
        private class ThicknessConverterInner : IValueConverter {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                if (!(value is Size)) return parameter;

                var scale = (Size)value;
                var thickness = parameter as Thickness? ?? new Thickness(1d);

                return new Thickness(thickness.Left / scale.Width, thickness.Top / scale.Height,
                        thickness.Right / scale.Width, thickness.Bottom / scale.Height);
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                if (!(value is Size)) return parameter;

                var scale = (Size)value;
                var thickness = parameter as Thickness? ?? new Thickness(1d);

                return new Thickness(thickness.Left * scale.Width, thickness.Top * scale.Height,
                        thickness.Right * scale.Width, thickness.Bottom * scale.Height);
            }
        }

        public static IValueConverter ThicknessConverter { get; } = new ThicknessConverterInner();

        private class InvertConverterInner : IValueConverter {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                return parameter.As(1d) / value.As(1d);
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                return parameter.As(1d) * value.As(1d);
            }
        }

        public static IValueConverter InvertConverter { get; } = new InvertConverterInner();
        #endregion

    }
}