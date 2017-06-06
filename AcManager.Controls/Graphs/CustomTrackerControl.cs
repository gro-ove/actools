using System;
using System.Windows;
using System.Windows.Controls.Primitives;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Wpf;

namespace AcManager.Controls.Graphs {
    public class CustomTrackerControl : TrackerControl {
        static CustomTrackerControl() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(CustomTrackerControl), new FrameworkPropertyMetadata(typeof(CustomTrackerControl)));
        }

        public static readonly DependencyProperty PositionOverrideProperty = DependencyProperty.Register(nameof(PositionOverride), typeof(ScreenPoint),
                typeof(CustomTrackerControl), new PropertyMetadata(OnPositionOverrideChanged));

        public ScreenPoint PositionOverride {
            get { return (ScreenPoint)GetValue(PositionOverrideProperty); }
            set { SetValue(PositionOverrideProperty, value); }
        }

        private static void OnPositionOverrideChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((CustomTrackerControl)o).UpdatePositionAndLineExtents();
        }

        public static readonly DependencyProperty LineExtentsOverrideProperty = DependencyProperty.Register(nameof(LineExtentsOverride), typeof(OxyRect),
                typeof(CustomTrackerControl), new PropertyMetadata(OnLineExtentsOverrideChanged));

        public OxyRect LineExtentsOverride {
            get { return (OxyRect)GetValue(LineExtentsOverrideProperty); }
            set { SetValue(LineExtentsOverrideProperty, value); }
        }

        private static void OnLineExtentsOverrideChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((CustomTrackerControl)o).UpdatePositionAndLineExtents();
        }

        private Popup _popup;

        public override void OnApplyTemplate() {
            UpdatePositionAndLineExtents();
            _popup = GetTemplateChild("PART_Popup") as Popup;
            base.OnApplyTemplate();
        }

        private void UpdatePositionAndLineExtents() {
            var position = PositionOverride;
            var extents = LineExtentsOverride;

            var yAxis = (DataContext as TrackerHitResult)?.YAxis;
            Position = position;
            LineExtents = yAxis?.Position == AxisPosition.Right
                    ? new OxyRect(position.X, position.Y, Math.Max(extents.Right - position.X, 0.001), Math.Max(extents.Bottom - position.Y, 0.001))
                    : new OxyRect(extents.Left, position.Y, Math.Max(position.X - extents.Left, 0.001), Math.Max(extents.Bottom - position.Y, 0.001));

            if (yAxis != null) {
                LineStroke = yAxis.AxislineColor.ToBrush();
            }
        }
    }
}