using System.Windows;
using System.Windows.Media;
using AcManager.Tools.Data;
using FirstFloor.ModernUI.Helpers;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Wpf;
using LinearAxis = OxyPlot.Axes.LinearAxis;

namespace AcManager.Controls.Graphs {
    public class CustomGraphViewer : GraphDataViewerBase {
        private const string KeyX = "x";
        private const string KeyY = "y";

        protected override void OnLoadedOverride() {
            base.OnLoadedOverride();
            Recreate();
        }

        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(nameof(Source), typeof(GraphData),
                typeof(CustomGraphViewer), new PropertyMetadata(OnSourceChanged));

        public GraphData Source {
            get => (GraphData)GetValue(SourceProperty);
            set => SetValue(SourceProperty, value);
        }

        private static void OnSourceChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((CustomGraphViewer)o).OnSourceChanged((GraphData)e.NewValue);
        }

        private void OnSourceChanged(GraphData newValue) {
            EnsureModelCreated();
            Model.Replace(KeyY, newValue);
            UpdateSteps(newValue?.MaxY ?? 0d);
            SetEmpty(newValue == null);
            InvalidatePlot();
        }

        public static readonly DependencyProperty XAxisTitleProperty = DependencyProperty.Register(nameof(XAxisTitle), typeof(string),
                typeof(CustomGraphViewer), new PropertyMetadata(OnXAxisTitleChanged));

        public string XAxisTitle {
            get => (string)GetValue(XAxisTitleProperty);
            set => SetValue(XAxisTitleProperty, value);
        }

        private static void OnXAxisTitleChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((CustomGraphViewer)o).Invalidate();
        }

        public static readonly DependencyProperty YAxisTitleProperty = DependencyProperty.Register(nameof(YAxisTitle), typeof(string),
                typeof(CustomGraphViewer), new PropertyMetadata(OnYAxisTitleChanged));

        public string YAxisTitle {
            get => (string)GetValue(YAxisTitleProperty);
            set => SetValue(YAxisTitleProperty, value);
        }

        private static void OnYAxisTitleChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((CustomGraphViewer)o).Invalidate();
        }

        public static readonly DependencyProperty ValueBrushProperty = DependencyProperty.Register(nameof(ValueBrush), typeof(Brush),
                typeof(CustomGraphViewer), new PropertyMetadata(OnValueBrushChanged));

        public Brush ValueBrush {
            get => (Brush)GetValue(ValueBrushProperty);
            set => SetValue(ValueBrushProperty, value);
        }

        private static void OnValueBrushChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((CustomGraphViewer)o).Invalidate();
        }

        public static readonly DependencyProperty TrackerFormatStringProperty = DependencyProperty.Register(nameof(TrackerFormatString), typeof(string),
                typeof(CustomGraphViewer), new PropertyMetadata("[b]{4:F1}[/b] at [b]{2:F1}[/b]", OnTrackerFormatStringChanged));

        public string TrackerFormatString {
            get => (string)GetValue(TrackerFormatStringProperty);
            set => SetValue(TrackerFormatStringProperty, value);
        }

        private static void OnTrackerFormatStringChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((CustomGraphViewer)o).Invalidate();
        }

        private readonly Busy _updateBusy = new Busy();

        private void Invalidate() {
            if (IsLoaded) {
                _updateBusy.DoDelay(Recreate, 1);
            }
        }

        private void Recreate() {
            Model = CreateModel();
            OnSourceChanged(Source);
        }

        protected override PlotModel CreateModel() {
            var valueColor = (ValueBrush as SolidColorBrush)?.Color.ToOxyColor() ?? BaseTextColor;
            return new PlotModel {
                TextColor = BaseTextColor,
                PlotAreaBorderColor = OxyColors.Transparent,
                LegendTextColor = BaseLegendTextColor,
                LegendPosition = LegendPosition.RightBottom,

                Axes = {
                    new LinearAxis {
                        Key = KeyX,
                        Title = XAxisTitle,
                        TextColor = BaseTextColor,
                        TitleColor = BaseTextColor,
                        TicklineColor = BaseTextColor,
                        AxislineColor = BaseTextColor,
                        Minimum = 0d,
                        Position = AxisPosition.Bottom
                    },
                    new LinearAxis {
                        Key = KeyY,
                        Title = YAxisTitle,
                        TextColor = valueColor,
                        TitleColor = valueColor,
                        TicklineColor = valueColor,
                        AxislineColor = valueColor,
                        Minimum = 0d,
                        Position = AxisPosition.Left
                    }
                },
                Series = {
                    new CatmulLineSeries {
                        Color = valueColor,
                        Title = YAxisTitle,
                        XAxisKey = KeyX,
                        YAxisKey = KeyY,
                        TrackerKey = KeyY,
                        TrackerFormatString = TrackerFormatString
                    }
                }
            };
        }
    }
}