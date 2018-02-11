using System;
using System.Collections.Generic;
using System.Windows;
using AcManager.Controls.Graphs;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;
using ConverterExtensions = OxyPlot.Wpf.ConverterExtensions;

namespace AcManager.Pages.Dialogs {
    public class TyresMachineGraphData {
        public ICollection<Tuple<string, double, ICollection<DataPoint>>> List;
        public List<Tuple<string, double, double, DataPoint>> ExtraPoints;
        public double LeftThreshold;
        public double RightThreshold;
        public DoubleRange YRange;
    }

    public class TyresMachineGraphViewer : GraphDataViewerBase {
        public static readonly DependencyProperty DataProperty = DependencyProperty.Register(nameof(Data),
                typeof(TyresMachineGraphData), typeof(TyresMachineGraphViewer), new PropertyMetadata(null, (o, e) => {
                    var v = (TyresMachineGraphViewer)o;
                    v._data = (TyresMachineGraphData)e.NewValue;
                    v.UpdateData();
                }));

        private TyresMachineGraphData _data;

        public TyresMachineGraphData Data {
            get => _data;
            set => SetValue(DataProperty, value);
        }

        private void UpdateData() {
            if (!IsLoaded) return;
            EnsureModelCreated();
            UpdateSeries();
        }

        public static readonly DependencyProperty ValueTitleProperty = DependencyProperty.Register(nameof(ValueTitle), typeof(string),
                typeof(TyresMachineGraphViewer), new PropertyMetadata(@"", (o, e) => {
                    var v = (TyresMachineGraphViewer)o;
                    v._valueTitle = (string)e.NewValue;
                    v.RefreshUnitParams();
                }));

        private string _valueTitle = @"";

        public string ValueTitle {
            get => _valueTitle;
            set => SetValue(ValueTitleProperty, value);
        }

        public static readonly DependencyProperty ValueUnitsProperty = DependencyProperty.Register(nameof(ValueUnits), typeof(string),
                typeof(TyresMachineGraphViewer), new PropertyMetadata(@"", (o, e) => {
                    var v = (TyresMachineGraphViewer)o;
                    v._valueUnits = (string)e.NewValue;
                    v.RefreshUnitParams();
                }));

        private string _valueUnits = @"";

        public string ValueUnits {
            get => _valueUnits;
            set => SetValue(ValueUnitsProperty, value);
        }

        public static readonly DependencyProperty ValueTitleDigitsProperty = DependencyProperty.Register(nameof(ValueTitleDigits), typeof(int),
                typeof(TyresMachineGraphViewer), new PropertyMetadata(2, (o, e) => {
                    var v = (TyresMachineGraphViewer)o;
                    v._valueTitleDigits = (int)e.NewValue;
                    v.RefreshUnitParams();
                }));

        private int _valueTitleDigits = 2;

        public int ValueTitleDigits {
            get => _valueTitleDigits;
            set => SetValue(ValueTitleDigitsProperty, value);
        }

        public static readonly DependencyProperty ValueTrackerDigitsProperty = DependencyProperty.Register(nameof(ValueTrackerDigits), typeof(int),
                typeof(TyresMachineGraphViewer), new PropertyMetadata(2, (o, e) => {
                    var v = (TyresMachineGraphViewer)o;
                    v._valueTrackerDigits = (int)e.NewValue;
                    v.RefreshUnitParams();
                }));

        private int _valueTrackerDigits = 2;

        public int ValueTrackerDigits {
            get => _valueTrackerDigits;
            set => SetValue(ValueTrackerDigitsProperty, value);
        }

        private static readonly string XUnit = " cm";
        private const string KeyRadius = "radius";
        private const string KeyValue = "value";

        private OxyColor _limitColor = OxyColor.FromUInt32(0x60ff0000);

        private void RefreshUnitParams() {
            UpdateSeries();
        }

        private readonly Busy _updateSeriesBusy = new Busy();

        private void UpdateSeries() {
            _updateSeriesBusy.DoDelay(() => {
                var model = Model;
                if (model == null) return;

                try {
                    var data = Data;
                    if (data == null) {
                        SetEmpty(true);
                        model.Series.Clear();
                        return;
                    }

                    var reuseSeries = data.List.Count == model.Series.Count;
                    if (!reuseSeries) {
                        _lineSeriesPool.AddAll(model.Series);
                    }

                    var x = new DoubleRange();
                    var index = 0;
                    foreach (var collection in data.List) {
                        var series = CreateLineSeries(collection, index, data.List.Count, reuseSeries ? (LineSeries)model.Series[index] : null, x);
                        index++;
                        if (!reuseSeries) {
                            model.Series.Add(series);
                        }
                    }

                    SetEmpty(!x.IsSet());

                    _leftThreshold.X = data.LeftThreshold;
                    _rightThreshold.X = data.RightThreshold;

                    if (data.ExtraPoints != null) {
                        for (var i = model.Annotations.Count - 1; i >= 0; i--) {
                            var item = model.Annotations[i];
                            if (!ReferenceEquals(item, _leftThreshold) && !ReferenceEquals(item, _rightThreshold)) {
                                _pointAnnotationsPool.AddFrom(model.Annotations, item);
                            }
                        }

                        for (var i = data.ExtraPoints.Count - 1; i >= 0; i--) {
                            var point = data.ExtraPoints[i];
                            var toolTipValue = string.Format($"{{0:F{ValueTrackerDigits}}}{ValueUnits} at {{1:F1}}{XUnit} (profile: {point.Item2:F1}{XUnit})",
                                    point.Item4.Y, point.Item4.X);
                            var annotation = (PointAnnotation)_pointAnnotationsPool.Get();
                            annotation.X = point.Item4.X;
                            annotation.Y = point.Item4.Y;
                            annotation.Fill = ProfileToColor(point.Item3, data.List.Count);
                            annotation.ToolTip = $"{point.Item1}: {toolTipValue}";
                            model.Annotations.Add(annotation);
                        }
                    }

                    var y = data.YRange;
                    if (y.IsSet()) {
                        var extraPadding = Math.Max(y.Range * 0.05, Math.Max(y.Maximum.Abs(), y.Minimum.Abs()) * 0.01);
                        _verticalAxis.Maximum = y.Maximum + extraPadding;
                        _verticalAxis.Minimum = y.Minimum - extraPadding;
                        _verticalAxis.Title = string.IsNullOrWhiteSpace(ValueUnits) ? ValueTitle : $@"{ValueTitle} ({ValueUnits.Trim()})";
                        _verticalAxis.StringFormat = $"0.{"#".RepeatString(ValueTitleDigits)}";

                        var step = GetStep((_verticalAxis.Maximum - _verticalAxis.Minimum).Abs());
                        _verticalAxis.MajorStep = step;
                        _verticalAxis.MinorStep = step / 5d;
                    }

                    if (x.IsSet()) {
                        _horizontalAxis.Minimum = x.Minimum;
                        _horizontalAxis.Maximum = x.Maximum;
                    }
                } finally {
                    InvalidatePlot();
                }

                OxyColor ProfileToColor(double value, int total) {
                    return value == -1 ? BaseTextColor : ConverterExtensions.ToOxyColor(
                            ColorExtension.FromHsb((180 + (total / 5d - 5).Saturate() * 180) * (1d - value.Saturate()), 1d, 1d));
                }

                LineSeries CreateLineSeries(Tuple<string, double, ICollection<DataPoint>> points, int index, int total, LineSeries result,
                        DoubleRange xRange) {
                    if (result != null) {
                        SyncPoints(result.Points, points.Item3, xRange);
                    } else {
                        result = (LineSeries)_lineSeriesPool.Get();
                        result.TrackerFormatString = $"[b]{{4:F{ValueTrackerDigits}}}{ValueUnits}[/b] at [b]{{2:F1}}{XUnit}[/b]";
                    }

                    var isInteresting = points.Item2 < 0d || index == 0
                            || Math.Abs(index - 2d * (total - total % 3) / 3d) < 0.0001
                            || Math.Abs(index - (total - total % 3) / 3d) < 0.0001
                            || index == total - 1;
                    result.Title = isInteresting ? points.Item1 : null;
                    result.Color = points.Item2 < 0d ? BaseTextColor : ProfileToColor(points.Item2, total);
                    SyncPoints(result.Points, points.Item3, xRange);
                    return result;
                }

                void SyncPoints(List<DataPoint> destination, ICollection<DataPoint> source, DoubleRange xRange) {
                    if (destination.Count == source.Count) {
                        if (source is DataPoint[] array) {
                            for (var j = array.Length - 1; j >= 0; j--) {
                                var p = array[j];
                                destination[j] = p;
                                xRange.Update(p.X);
                            }
                        } else if (source is IReadOnlyList<DataPoint> list) {
                            for (var j = list.Count - 1; j >= 0; j--) {
                                var p = list[j];
                                destination[j] = p;
                                xRange.Update(p.X);
                            }
                        } else {
                            var i = 0;
                            foreach (var p in source) {
                                destination[i++] = p;
                                xRange.Update(p.X);
                            }
                        }

                    } else {
                        destination.Clear();
                        destination.AddRange(source);

                        if (source is DataPoint[] array) {
                            for (var j = array.Length - 1; j >= 0; j--) {
                                xRange.Update(array[j].X);
                            }
                        } else if (source is IReadOnlyList<DataPoint> list) {
                            for (var j = list.Count - 1; j >= 0; j--) {
                                xRange.Update(list[j].X);
                            }
                        } else {
                            foreach (var p in source) {
                                xRange.Update(p.X);
                            }
                        }
                    }
                }
            }, 10);
        }

        private PoolConstruct<Series> _lineSeriesPool = new PoolConstruct<Series>(() => new LineSeries {
            TrackerKey = KeyValue
        });

        private PoolConstruct<Annotation> _pointAnnotationsPool = new PoolConstruct<Annotation>(() => new PointAnnotation {
            Size = 3
        });

        private LinearAxis _verticalAxis, _horizontalAxis;
        private LineAnnotation _leftThreshold, _rightThreshold;

        protected override PlotModel CreateModel() {
            _verticalAxis = new LinearAxis {
                Key = KeyValue,
                Title = ValueUnits,
                TextColor = BaseTextColor,
                TitleColor = BaseTextColor,
                TicklineColor = BaseTextColor,
                AxislineColor = BaseTextColor,
                Minimum = 0d,
                Position = AxisPosition.Left,
                AxisDistance = 2,
                IsPanEnabled = false,
                AxisTitleDistance = 8,
                MaximumPadding = 0,
                MinimumPadding = 0,
                AxisTickToLabelDistance = 2,
                MajorTickSize = 4,
                MinorTickSize = 1
            };

            _horizontalAxis = new LinearAxis {
                Key = KeyRadius,
                Title = $"Radius ({XUnit.Trim()})",
                TextColor = BaseTextColor,
                TitleColor = BaseTextColor,
                TicklineColor = BaseTextColor,
                AxislineColor = BaseTextColor,
                Minimum = 0d,
                Position = AxisPosition.Bottom,
                AxisDistance = 2,
                IsPanEnabled = false,
                AxisTitleDistance = 8,
                MaximumPadding = 0,
                MinimumPadding = 0,
                AxisTickToLabelDistance = 2,
                MajorTickSize = 4,
                MinorTickSize = 1
            };

            _leftThreshold = new LineAnnotation {
                Type = LineAnnotationType.Vertical,
                LineStyle = LineStyle.Dash,
                Color = _limitColor
            };

            _rightThreshold = new LineAnnotation {
                Type = LineAnnotationType.Vertical,
                LineStyle = LineStyle.Dash,
                Color = _limitColor,
            };

            return new PlotModel {
                TextColor = BaseTextColor,
                PlotAreaBorderColor = OxyColors.Transparent,
                LegendTextColor = BaseTextColor,
                LegendPosition = LegendPosition.RightBottom,
                Padding = new OxyThickness(0d),
                LegendPadding = 0d,
                TitlePadding = 0d,
                LegendMargin = 0d,
                PlotMargins = new OxyThickness(40d, 0d, 40d, 32d),
                Axes = {
                    _horizontalAxis,
                    _verticalAxis
                },
                Annotations = {
                    _leftThreshold,
                    _rightThreshold
                }
            };
        }

        protected override void OnLoadedOverride() {
            base.OnLoadedOverride();
            LoadColor(ref _limitColor, "CarPowerColor", 0x60);
            EnsureModelCreated();
            UpdateSeries();
        }
    }
}