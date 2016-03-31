using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AcManager.Tools.Objects;

namespace AcManager.Controls {
    public partial class GraphViewer : UserControl {
        public static readonly DependencyProperty SourceProperty =
            DependencyProperty.Register("Source", typeof(GraphData), typeof(GraphViewer), new PropertyMetadata(OnSourceChanged));

        private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            ((GraphViewer)d).OnSourceChanged((GraphData)e.OldValue, (GraphData)e.NewValue);
        }

        private void OnSourceChanged(GraphData oldValue, GraphData newValue) {
            UpdateCanvas(newValue);
        }

        public GraphData Source {
            get { return (GraphData)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public GraphViewer() {
            InitializeComponent();
        }

        private void UpdateCanvas(GraphData value = null) {
            GraphCanvas.Children.Clear();

            var source = value ?? Source;
            if (source == null) return;

            var points = source.NormalizedValuesArray;

            var padding = 20;
            var width = ActualWidth - padding * 2;
            var height = ActualHeight - padding * 2;

            var lineGroup = new GeometryGroup();
            for (var i = 1; i < points.Count; i++) {
                var p0 = points[i - 1];
                var p1 = points[i];
                lineGroup.Children.Add(new LineGeometry(
                        new Point(p0.X * width + padding, (1.0 - p0.Y) * height + padding),
                        new Point(p1.X * width + padding, (1.0 - p1.Y) * height + padding)));
            }
            GraphCanvas.Children.Add(new Path {
                StrokeThickness = 2,
                Stroke = Foreground,
                Data = lineGroup
            });

            var graphLinesGroup = new GeometryGroup();
            graphLinesGroup.Children.Add(new LineGeometry(
                    new Point(padding, padding),
                    new Point(width + padding, height)));
            graphLinesGroup.Children.Add(new LineGeometry(
                    new Point(padding, padding),
                    new Point(width, height + padding)));
            GraphCanvas.Children.Add(new Path {
                StrokeThickness = 1,
                Stroke = Brushes.LightSlateGray,
                
                Data = graphLinesGroup
            });
        }

        public override void OnApplyTemplate() {
            base.OnApplyTemplate();
            UpdateCanvas();
        }
    }
}
