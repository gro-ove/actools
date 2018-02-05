using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using AcTools.Utils;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Wpf;
using PlotCommands = OxyPlot.PlotCommands;

namespace AcManager.Controls.Graphs {
    public abstract class GraphDataViewerBase : PlotView {
        private class CustomController : ControllerBase, IPlotController {
            public CustomController() {
                this.BindMouseDown(OxyMouseButton.Left, PlotCommands.PointsOnlyTrack);
                this.BindMouseDown(OxyMouseButton.Left, OxyModifierKeys.Shift, PlotCommands.Track);
            }
        }

        protected GraphDataViewerBase() {
            Controller = new CustomController();
            Loaded += OnLoaded;
        }

        protected void LoadColor(ref OxyColor color, string key) {
            var r = TryFindResource(key);
            var v = r as Color? ?? (r as SolidColorBrush)?.Color;
            if (v != null) {
                color = v.Value.ToOxyColor();
            }
        }

        private OxyColor _textColor = OxyColor.FromUInt32(0xffffffff);
        protected OxyColor BaseTextColor => _textColor;
        protected OxyColor BaseLegendTextColor => OxyColor.FromArgb((_textColor.A * 0.67).ClampToByte(), _textColor.R, _textColor.G, _textColor.B);

        private void OnLoaded(object sender, RoutedEventArgs routedEventArgs) {
            SetValue(TextOptions.TextFormattingModeProperty, TextFormattingMode.Ideal);
            SetValue(TextOptions.TextHintingModeProperty, TextHintingMode.Fixed);
            SetValue(TextOptions.TextRenderingModeProperty, TextRenderingMode.Grayscale);
            LoadColor(ref _textColor, "WindowText");
            OnLoadedOverride();
        }

        protected virtual void OnLoadedOverride() {}

        static GraphDataViewerBase() {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(GraphDataViewerBase), new FrameworkPropertyMetadata(typeof(GraphDataViewerBase)));
        }

        protected abstract PlotModel CreateModel();

        protected void EnsureModelCreated() {
            if (Model == null) {
                Model = CreateModel();
            }
        }

        private static IEnumerable<double> Steps() {
            for (var i = 0; i < 10; i++) {
                var v = Math.Pow(10d, i - 2);
                yield return v;
                yield return v * 2d;
                yield return v * 4d;
                yield return v * 5d;
            }
        }

        private static double GetStep(double maxValue) {
            if (maxValue < 1d || maxValue > 1e6) return double.NaN;

            foreach (var v in Steps()) {
                var a = maxValue / v;
                if (a >= 2d && a < 4d) return v;
            }

            return Math.Pow(10d, Math.Floor(Math.Log10(maxValue)));
        }

        protected void UpdateSteps(double maximumValue) {
            foreach (var axis in Model.Axes.Where(x => x.Position != AxisPosition.Bottom)) {
                axis.Maximum = maximumValue * 1.05;

                var step = GetStep(axis.Maximum);
                axis.MajorStep = step;
                axis.MinorStep = step / 5d;
            }
        }

        public static readonly DependencyPropertyKey IsEmptyPropertyKey = DependencyProperty.RegisterReadOnly(nameof(IsEmpty), typeof(bool),
                typeof(GraphDataViewerBase), new PropertyMetadata(false));

        public static readonly DependencyProperty IsEmptyProperty = IsEmptyPropertyKey.DependencyProperty;

        public bool IsEmpty => GetValue(IsEmptyProperty) as bool? == true;

        protected void SetEmpty(bool isEmpty) {
            SetValue(IsEmptyPropertyKey, isEmpty);
        }
    }
}