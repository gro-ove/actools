using AcManager.Tools.Objects;
using System.Windows;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Wpf;
using LinearAxis = OxyPlot.Axes.LinearAxis;
using LineSeries = OxyPlot.Series.LineSeries;

namespace AcManager.Controls {
    public class OxyPlotGraphViewer : PlotView {
        static OxyPlotGraphViewer() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(OxyPlotGraphViewer), new FrameworkPropertyMetadata(typeof(OxyPlotGraphViewer)));
        }

        public static readonly DependencyProperty SourceTorqueProperty =
            DependencyProperty.Register(nameof(SourceTorque), typeof(GraphData), typeof(OxyPlotGraphViewer), new PropertyMetadata(OnSourceTorqueChanged));

        private static void OnSourceTorqueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            ((OxyPlotGraphViewer)d).UpdateCanvas();
        }

        public static readonly DependencyProperty SourcePowerProperty =
            DependencyProperty.Register(nameof(SourcePower), typeof(GraphData), typeof(OxyPlotGraphViewer), new PropertyMetadata(OnSourcePowerChanged));

        private static void OnSourcePowerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            ((OxyPlotGraphViewer)d).UpdateCanvas();
        }

        public GraphData SourceTorque {
            get { return (GraphData)GetValue(SourceTorqueProperty); }
            set { SetValue(SourceTorqueProperty, value); }
        }

        public GraphData SourcePower {
            get { return (GraphData)GetValue(SourcePowerProperty); }
            set { SetValue(SourcePowerProperty, value); }
        }

        private const string KeyRpm = "rpm";
        private const string KeyBhp = "bhp";
        private const string KeyNm = "nm";

        private void UpdateCanvas() {
            // TODO: find the way without double redrawing on initialization
            
            var powerColor = OxyColor.FromUInt32(0xffff0000);
            var torqueColor = OxyColor.FromUInt32(0xffffff00);

            var model = new PlotModel {
                TextColor = OxyColor.FromUInt32(0xffffffff),
                PlotAreaBorderColor = OxyColors.Transparent,
                LegendTextColor = OxyColor.FromUInt32(0x88ffffff),
                LegendPosition = LegendPosition.RightBottom
            };

            model.Axes.Add(new LinearAxis {
                Key = KeyRpm,
                TicklineColor = OxyColors.White,
                Position = AxisPosition.Bottom
            });

            model.Axes.Add(new LinearAxis {
                Key = KeyBhp,
                Title = Tools.Resources.Units_BHP,
                TextColor = powerColor,
                TitleColor = powerColor,
                TicklineColor = powerColor,
                Position = AxisPosition.Right
            });

            model.Axes.Add(new LinearAxis {
                Key = KeyNm,
                Title = Tools.Resources.Units_Nm,
                TextColor = torqueColor,
                TitleColor = torqueColor,
                TicklineColor = torqueColor,
                Position = AxisPosition.Left
            });

            var sourcePower = SourcePower;
            if (sourcePower != null) {
                var powerLineSeries = new LineSeries {
                    Color = powerColor,
                    Title = Tools.Resources.Common_Power,
                    XAxisKey = KeyRpm,
                    YAxisKey = KeyBhp,
                    TrackerKey = KeyBhp
                };
                foreach (var point in sourcePower.Values) {
                    powerLineSeries.Points.Add(new DataPoint(point.Key, point.Value));
                }
                model.Series.Add(powerLineSeries);
            }
            
            var sourceTorque = SourceTorque;
            if (sourceTorque != null) {
                var torqueLineSeries = new LineSeries {
                    Color = torqueColor,
                    Title = Tools.Resources.Common_Torque,
                    XAxisKey = KeyRpm,
                    YAxisKey = KeyNm,
                    TrackerKey = KeyNm
                };
                foreach (var point in SourceTorque.Values) {
                    torqueLineSeries.Points.Add(new DataPoint(point.Key, point.Value));
                }
                model.Series.Add(torqueLineSeries);
            }

            Model = model;
        }
    }
}
