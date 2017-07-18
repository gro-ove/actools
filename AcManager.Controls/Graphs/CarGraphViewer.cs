using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using AcManager.Tools;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Objects;
using AcTools.Utils;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Converters;
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
            get => (CarObject)GetValue(CarProperty);
            set => SetValue(CarProperty, value);
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
            get => (GraphData)GetValue(SourceTorqueProperty);
            set => SetValue(SourceTorqueProperty, value);
        }

        [CanBeNull]
        public GraphData SourcePower {
            get => (GraphData)GetValue(SourcePowerProperty);
            set => SetValue(SourcePowerProperty, value);
        }

        private const string KeyRpm = "rpm";
        private const string KeyBhp = "bhp";
        private const string KeyNm = "nm";

        private OxyColor _powerColor = OxyColor.FromUInt32(0xffff0000);
        private OxyColor _torqueColor = OxyColor.FromUInt32(0xffffff00);

        protected override PlotModel CreateModel() {
            SettingsHolder.Content.SubscribeWeak(OnContentSettingsChanged);
            return new PlotModel {
                TextColor = BaseTextColor,
                PlotAreaBorderColor = OxyColors.Transparent,
                LegendTextColor = BaseLegendTextColor,
                LegendPosition = LegendPosition.RightBottom,

                Padding = new OxyThickness(0d),
                LegendPadding = 0d,
                TitlePadding = 0d,
                LegendMargin = 0d,
                PlotMargins = new OxyThickness(40d, 0d, 40d, 32d),

                Axes = {
                    new LinearAxis {
                        Key = KeyRpm,
                        Title = ToolsStrings.Units_RPM,
                        TextColor = BaseTextColor,
                        TitleColor = BaseTextColor,
                        TicklineColor = BaseTextColor,
                        AxislineColor = BaseTextColor,
                        Minimum = 0d,
                        Position = AxisPosition.Bottom
                    },
                    new LinearAxis {
                        Key = KeyBhp,
                        Title = ToolsStrings.Units_BHP,
                        TextColor = _powerColor,
                        TitleColor = _powerColor,
                        TicklineColor = _powerColor,
                        AxislineColor = _powerColor,
                        Minimum = 0d,
                        Position = AxisPosition.Right
                    },
                    new LinearAxis {
                        Key = KeyNm,
                        Title = ToolsStrings.Units_Nm,
                        TextColor = _torqueColor,
                        TitleColor = _torqueColor,
                        TicklineColor = _torqueColor,
                        AxislineColor = _torqueColor,
                        Minimum = 0d,
                        Position = AxisPosition.Left
                    }
                },

                Series = {
                    new CatmulLineSeries {
                        Color = _powerColor,
                        Title = ToolsStrings.Common_Power,
                        XAxisKey = KeyRpm,
                        YAxisKey = KeyBhp,
                        TrackerKey = KeyBhp,
                        TrackerFormatString = $"[b]{{4:F0}} {ToolsStrings.Units_BHP}[/b] at [b]{{2:F0}} {ToolsStrings.Units_RPM}[/b]"
                    },
                    new CatmulLineSeries {
                        Color = _torqueColor,
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
            if (!IsLoaded) return;
            EnsureModelCreated();
            Model.Replace(KeyBhp, SourcePower);
            UpdateMaximumValues();
            InvalidatePlot();
        }

        private void UpdateTorque() {
            if (!IsLoaded) return;
            EnsureModelCreated();
            Model.Replace(KeyNm, SourceTorque);
            UpdateMaximumValues();
            InvalidatePlot();
        }

        protected override void OnLoadedOverride() {
            base.OnLoadedOverride();

            LoadColor(ref _powerColor, "CarPowerColor");
            LoadColor(ref _torqueColor, "CarTorqueColor");

            EnsureModelCreated();
            Model.Replace(KeyBhp, SourcePower);
            Model.Replace(KeyNm, SourceTorque);
            UpdateMaximumValues();
            InvalidatePlot();
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
