using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using JetBrains.Annotations;
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

        public OxyPlotGraphViewer() {
            /*LegendTitleColor = Colors.Aquamarine;

            SetValue(SelectionColorProperty, Colors.BlueViolet);
            SubtitleColor = Colors.Magenta;
            TitleColor = Colors.GreenYellow;

            LegendTextColor = Colors.CornflowerBlue;
            PlotAreaBorderColor = Colors.Orange;
            TextColor = Colors.OrangeRed;*/

            
        }

        public static readonly DependencyProperty SourceTorqueProperty =
            DependencyProperty.Register(nameof(SourceTorque), typeof(GraphData), typeof(OxyPlotGraphViewer), new PropertyMetadata(OnSourceTorqueChanged));

        private static void OnSourceTorqueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            ((OxyPlotGraphViewer)d).UpdateTorque();
        }

        public static readonly DependencyProperty SourcePowerProperty =
            DependencyProperty.Register(nameof(SourcePower), typeof(GraphData), typeof(OxyPlotGraphViewer), new PropertyMetadata(OnSourcePowerChanged));

        private static void OnSourcePowerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            ((OxyPlotGraphViewer)d).UpdatePower();
        }

        [CanBeNull]
        public GraphData SourceTorque {
            get { return (GraphData)GetValue(SourceTorqueProperty); }
            set { SetValue(SourceTorqueProperty, value); }
        }

        [CanBeNull]
        public GraphData SourcePower {
            get { return (GraphData)GetValue(SourcePowerProperty); }
            set { SetValue(SourcePowerProperty, value); }
        }

        private const string KeyRpm = "rpm";
        private const string KeyBhp = "bhp";
        private const string KeyNm = "nm";

        private static readonly OxyColor PowerColor = OxyColor.FromUInt32(0xffff0000);
        private static readonly OxyColor TorqueColor = OxyColor.FromUInt32(0xffffff00);

        private void CreateModel() {
            // TrackerDefinitions

            Model = new PlotModel {
                TextColor = OxyColor.FromUInt32(0xffffffff),
                PlotAreaBorderColor = OxyColors.Transparent,
                LegendTextColor = OxyColor.FromUInt32(0x88ffffff),
                LegendPosition = LegendPosition.RightBottom,

                Axes = {
                    new LinearAxis {
                        Key = KeyRpm,
                        TicklineColor = OxyColors.White,
                        Position = AxisPosition.Bottom
                    },
                    new LinearAxis {
                        Key = KeyBhp,
                        Title = Tools.ToolsStrings.Units_BHP,
                        TextColor = PowerColor,
                        TitleColor = PowerColor,
                        TicklineColor = PowerColor,
                        Position = AxisPosition.Right
                    },
                    new LinearAxis {
                        Key = KeyNm,
                        Title = Tools.ToolsStrings.Units_Nm,
                        TextColor = TorqueColor,
                        TitleColor = TorqueColor,
                        TicklineColor = TorqueColor,
                        Position = AxisPosition.Left
                    }
                },

                Series = {
                    new LineSeries {
                        Color = PowerColor,
                        Title = Tools.ToolsStrings.Common_Power,
                        XAxisKey = KeyRpm,
                        YAxisKey = KeyBhp,
                        TrackerKey = KeyBhp,

                        Smooth = SettingsHolder.Content.SmoothCurves,
                        CanTrackerInterpolatePoints = SettingsHolder.Content.SmoothCurves
                    },
                    new LineSeries {
                        Color = TorqueColor,
                        Title = Tools.ToolsStrings.Common_Torque,
                        XAxisKey = KeyRpm,
                        YAxisKey = KeyNm,
                        TrackerKey = KeyNm,

                        Smooth = SettingsHolder.Content.SmoothCurves,
                        CanTrackerInterpolatePoints = SettingsHolder.Content.SmoothCurves,
                    }
                }
            };

            WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.AddHandler(SettingsHolder.Content, nameof(INotifyPropertyChanged.PropertyChanged),
                    ContentSettings_Changed);
        }

        private void ContentSettings_Changed(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName != nameof(SettingsHolder.Content.SmoothCurves) || Model == null) return;
            foreach (var series in Model.Series.OfType<LineSeries>()) {
                series.Smooth = SettingsHolder.Content.SmoothCurves;
            }
            InvalidatePlot();
        }

        private void EnsureModelCreated() {
            if (Model == null) {
                CreateModel();
            }
        }

        private void UpdatePower() {
            EnsureModelCreated();
            Model.Replace(KeyBhp, SourcePower);
            UpdateMaximumValues();
            InvalidatePlot();
        }

        private void UpdateTorque() {
            EnsureModelCreated();
            Model.Replace(KeyNm, SourceTorque);
            UpdateMaximumValues();
            InvalidatePlot();
        }

        private void UpdateMaximumValues() {
            var maximumValue = Math.Max(SourcePower?.Points.Values.Max() ?? 0d, SourceTorque?.Points.Values.Max() ?? 0d);
            foreach (var axis in Model.Axes.Where(x => x.Position != AxisPosition.Bottom)) {
                axis.Maximum = maximumValue * 1.05;
            }
        }
    }

    internal static class OxyExtenstion {
        public static void Replace(this PlotModel collection, string trackerKey, GraphData data) {
            var series = collection.Series.OfType<LineSeries>().FirstOrDefault(x => x.TrackerKey == trackerKey);
            if (series == null) return;

            series.Points.Clear();
            if (data != null) {
                series.Points.AddRange(data.Points.Select(x => new DataPoint(x.Key, x.Value)));
            }
        }
    }
}
