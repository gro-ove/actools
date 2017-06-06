using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using AcManager.Tools;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using FirstFloor.ModernUI;
using JetBrains.Annotations;
using OxyPlot;
using OxyPlot.Axes;
using LinearAxis = OxyPlot.Axes.LinearAxis;
using LineSeries = OxyPlot.Series.LineSeries;

namespace AcManager.Controls.Graphs {
    public class CarGraphViewer : GraphDataViewerBase {
        public static readonly DependencyProperty SourceTorqueProperty =
            DependencyProperty.Register(nameof(SourceTorque), typeof(GraphData), typeof(CarGraphViewer), new PropertyMetadata(OnSourceTorqueChanged));

        private static void OnSourceTorqueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            ((CarGraphViewer)d).UpdateTorque();
        }

        public static readonly DependencyProperty SourcePowerProperty =
            DependencyProperty.Register(nameof(SourcePower), typeof(GraphData), typeof(CarGraphViewer), new PropertyMetadata(OnSourcePowerChanged));

        private static void OnSourcePowerChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            ((CarGraphViewer)d).UpdatePower();
        }

        public static readonly DependencyProperty CarProperty = DependencyProperty.Register(nameof(Car), typeof(CarObject),
                typeof(CarGraphViewer), new PropertyMetadata(OnCarChanged));

        public CarObject Car {
            get { return (CarObject)GetValue(CarProperty); }
            set { SetValue(CarProperty, value); }
        }

        private static void OnCarChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((CarGraphViewer)o).OnCarChanged((CarObject)e.NewValue);
        }

        private void OnCarChanged(CarObject newValue) {
            if (newValue != null) {
                SourcePower = newValue.SpecsPowerCurve;
                SourceTorque = newValue.SpecsTorqueCurve;
            }
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

        protected override PlotModel CreateModel() {
            SettingsHolder.Content.SubscribeWeak(OnContentSettingsChanged);
            return new PlotModel {
                TextColor = OxyColor.FromUInt32(0xffffffff),
                PlotAreaBorderColor = OxyColors.Transparent,
                LegendTextColor = OxyColor.FromUInt32(0x88ffffff),
                LegendPosition = LegendPosition.RightBottom,

                Axes = {
                    new LinearAxis {
                        Key = KeyRpm,
                        Title = ToolsStrings.Units_RPM,
                        TextColor = OxyColors.White,
                        TitleColor = OxyColors.White,
                        TicklineColor = OxyColors.White,
                        AxislineColor = OxyColors.White,
                        Minimum = 0d,
                        Position = AxisPosition.Bottom
                    },
                    new LinearAxis {
                        Key = KeyBhp,
                        Title = ToolsStrings.Units_BHP,
                        TextColor = PowerColor,
                        TitleColor = PowerColor,
                        TicklineColor = PowerColor,
                        AxislineColor = PowerColor,
                        Minimum = 0d,
                        Position = AxisPosition.Right
                    },
                    new LinearAxis {
                        Key = KeyNm,
                        Title = ToolsStrings.Units_Nm,
                        TextColor = TorqueColor,
                        TitleColor = TorqueColor,
                        TicklineColor = TorqueColor,
                        AxislineColor = TorqueColor,
                        Minimum = 0d,
                        Position = AxisPosition.Left
                    }
                },

                Series = {
                    new CatmulLineSeries {
                        Color = PowerColor,
                        Title = ToolsStrings.Common_Power,
                        XAxisKey = KeyRpm,
                        YAxisKey = KeyBhp,
                        TrackerKey = KeyBhp,
                        TrackerFormatString = $"[b]{{4:F0}} {ToolsStrings.Units_BHP}[/b] at [b]{{2:F0}} {ToolsStrings.Units_RPM}[/b]"
                    },
                    new CatmulLineSeries {
                        Color = TorqueColor,
                        Title = ToolsStrings.Common_Torque,
                        XAxisKey = KeyRpm,
                        YAxisKey = KeyNm,
                        TrackerKey = KeyNm,
                        TrackerFormatString = $"[b]{{4:F0}} {ToolsStrings.Units_Nm}[/b] at [b]{{2:F0}} {ToolsStrings.Units_RPM}[/b]"
                    }
                }
            };
        }

        private void OnContentSettingsChanged(object sender, PropertyChangedEventArgs e) {
            if (e.PropertyName != nameof(SettingsHolder.Content.SmoothCurves) || Model == null) return;
            foreach (var series in Model.Series.OfType<LineSeries>()) {
                series.Smooth = SettingsHolder.Content.SmoothCurves;
            }
            InvalidatePlot();
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

        private static IEnumerable<double> Steps() {
            for (var i = 0; i < 10; i++) {
                var v = Math.Pow(10d, i - 1);
                yield return v;
                yield return v * 2d;
                yield return v * 4d;
                yield return v * 5d;
            }
        }

        private void UpdateMaximumValues() {
            var power = SourcePower;
            var torque = SourceTorque;
            var maximumValue = Math.Max(power?.MaxY ?? 0d, torque?.MaxY ?? 0d);
            UpdateSteps(maximumValue);
            SetEmpty(!(power?.Points.Count > 1 || torque?.Points.Count > 1));
        }
    }
}
