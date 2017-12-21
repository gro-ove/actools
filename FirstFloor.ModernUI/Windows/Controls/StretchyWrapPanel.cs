using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class StretchyWrapPanel : Panel {
        public static readonly DependencyProperty ItemWidthProperty = DependencyProperty.Register(nameof(ItemWidth), typeof(double),
                typeof(StretchyWrapPanel), new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure, (o, e) => {
                    ((StretchyWrapPanel)o)._itemWidth = (double)e.NewValue;
                }));

        private double _itemWidth = double.NaN;

        [TypeConverter(typeof(LengthConverter))]
        public double ItemWidth {
            get => _itemWidth;
            set => SetValue(ItemWidthProperty, value);
        }

        public static readonly DependencyProperty ItemHeightProperty = DependencyProperty.Register(nameof(ItemHeight), typeof(double),
                typeof(StretchyWrapPanel), new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure, (o, e) => {
                    ((StretchyWrapPanel)o)._itemHeight = (double)e.NewValue;
                }));

        private double _itemHeight = double.NaN;

        [TypeConverter(typeof(LengthConverter))]
        public double ItemHeight {
            get => _itemHeight;
            set => SetValue(ItemHeightProperty, value);
        }

        public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(nameof(Orientation), typeof(Orientation),
                typeof(StretchyWrapPanel), new FrameworkPropertyMetadata(Orientation.Horizontal, FrameworkPropertyMetadataOptions.AffectsArrange, (o, e) => {
                    ((StretchyWrapPanel)o)._orientation = (Orientation)e.NewValue;
                }));

        private Orientation _orientation = Orientation.Horizontal;

        public Orientation Orientation {
            get => _orientation;
            set => SetValue(OrientationProperty, value);
        }

        public static readonly DependencyProperty StretchToFillProperty = DependencyProperty.Register(nameof(StretchToFill), typeof(bool),
                typeof(StretchyWrapPanel), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsArrange, (o, e) => {
                    ((StretchyWrapPanel)o)._stretchToFill = (bool)e.NewValue;
                }));

        private bool _stretchToFill = true;

        public bool StretchToFill {
            get => _stretchToFill;
            set => SetValue(StretchToFillProperty, value);
        }

        public static readonly DependencyProperty StretchProportionallyProperty = DependencyProperty.Register(nameof(StretchProportionally), typeof(bool),
                typeof(StretchyWrapPanel), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.AffectsArrange, (o, e) => {
                    ((StretchyWrapPanel)o)._stretchProportionally = (bool)e.NewValue;
                }));

        private bool _stretchProportionally = true;

        public bool StretchProportionally {
            get => _stretchProportionally;
            set => SetValue(StretchProportionallyProperty, value);
        }

        public static readonly DependencyProperty RearrangeForBestFitProperty = DependencyProperty.Register(nameof(RearrangeForBestFit), typeof(bool),
                typeof(StretchyWrapPanel), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsMeasure, (o, e) => {
                    ((StretchyWrapPanel)o)._rearrangeForBestFit = (bool)e.NewValue;
                }));

        private bool _rearrangeForBestFit;

        public bool RearrangeForBestFit {
            get => _rearrangeForBestFit;
            set => SetValue(RearrangeForBestFitProperty, value);
        }

        private struct UVSize {
            internal UVSize(Orientation orientation, Size size) {
                U = V = 0d;
                _isHorizontal = orientation == Orientation.Horizontal;
                Width = size.Width;
                Height = size.Height;
            }

            internal UVSize(Orientation orientation, double width, double height) {
                U = V = 0d;
                _isHorizontal = orientation == Orientation.Horizontal;
                Width = width;
                Height = height;
            }

            internal UVSize(Orientation orientation) {
                U = V = 0d;
                _isHorizontal = orientation == Orientation.Horizontal;
            }

            internal double U;
            internal double V;
            private bool _isHorizontal;

            internal double Width {
                get => _isHorizontal ? U : V;
                set {
                    if (_isHorizontal) {
                        U = value;
                    } else {
                        V = value;
                    }
                }
            }

            internal double Height {
                get => _isHorizontal ? V : U;
                set {
                    if (_isHorizontal) {
                        V = value;
                    } else {
                        U = value;
                    }
                }
            }
        }

        protected override Size MeasureOverride(Size constraint) {
            return RearrangeForBestFit ? MeasureBestFit(constraint) : MeasureKeepInOrder(constraint);
        }

        private Size MeasureKeepInOrder(Size constraint) {
            var orientation = Orientation;
            var uLimit = new UVSize(orientation, constraint.Width, constraint.Height).U;
            var curLineSize = new UVSize(orientation);
            var panelSize = new UVSize(orientation);
            var itemWidth = ItemWidth;
            var itemHeight = ItemHeight;
            var itemWidthSet = !double.IsNaN(itemWidth);
            var itemHeightSet = !double.IsNaN(itemHeight);

            var childConstraint = new Size(
                    itemWidthSet ? itemWidth : constraint.Width,
                    itemHeightSet ? itemHeight : constraint.Height);

            var children = InternalChildren;

            for (int i = 0, count = children.Count; i < count; i++) {
                var child = children[i];
                if (child == null) continue;

                // Flow passes its own constrint to children
                child.Measure(childConstraint);

                // This is the size of the child in UV space
                var sz = new UVSize(orientation,
                        itemWidthSet ? itemWidth : child.DesiredSize.Width,
                        itemHeightSet ? itemHeight : child.DesiredSize.Height);

                if (curLineSize.U + sz.U > uLimit) {
                    // Need to switch to another line
                    panelSize.U = Math.Max(curLineSize.U, panelSize.U);
                    panelSize.V += curLineSize.V;
                    curLineSize = sz;

                    if (sz.U > uLimit) {
                        // The element is wider then the constrint - give it a separate line
                        panelSize.U = Math.Max(sz.U, panelSize.U);
                        panelSize.V += sz.V;
                        curLineSize = new UVSize(orientation);
                    }
                } else {
                    // Continue to accumulate a line
                    curLineSize.U += sz.U;
                    curLineSize.V = Math.Max(sz.V, curLineSize.V);
                }
            }

            // The last line size, if any should be added
            panelSize.U = Math.Max(curLineSize.U, panelSize.U);
            panelSize.V += curLineSize.V;

            // Go from UV space to W/H space
            return new Size(panelSize.Width, panelSize.Height);
        }

        private Size MeasureBestFit(Size constraint) {
            var orientation = Orientation;
            var uLimit = new UVSize(orientation, constraint.Width, constraint.Height).U;
            var itemWidth = ItemWidth;
            var itemHeight = ItemHeight;
            var itemWidthSet = !double.IsNaN(itemWidth);
            var itemHeightSet = !double.IsNaN(itemHeight);

            var childConstraint = new Size(
                    itemWidthSet ? itemWidth : constraint.Width,
                    itemHeightSet ? itemHeight : constraint.Height);

            var children = InternalChildren;

            // First-Fit Decreasing Height (FFDH) algorithm
            var lines = new List<UVSize>();

            for (int i = 0, count = children.Count; i < count; i++) {
                var child = children[i];
                if (child == null) continue;

                // Flow passes its own constrint to children
                child.Measure(childConstraint);

                // This is the size of the child in UV space
                var childSize = new UVSize(orientation,
                        itemWidthSet ? itemWidth : child.DesiredSize.Width,
                        itemHeightSet ? itemHeight : child.DesiredSize.Height);

                for (var j = 0; j < lines.Count; j++) {
                    var line = lines[j];
                    if (line.U + childSize.U <= uLimit) {
                        lines[j] = new UVSize(orientation) { U = childSize.U, V = Math.Max(childSize.V, line.V) };
                        goto Next;
                    }
                }

                lines.Add(childSize);

                Next:
                { }
            }

            var panelSize = new UVSize(orientation);
            for (var i = 0; i < lines.Count; i++) {
                var line = lines[i];
                panelSize.U = Math.Max(line.U, panelSize.U);
                panelSize.V += line.V;
            }

            // Go from UV space to W/H space
            return new Size(panelSize.Width, panelSize.Height);
        }

        protected override Size ArrangeOverride(Size finalSize) {
            return RearrangeForBestFit ? ArrangeBestFit(finalSize) : ArrangeKeepInOrder(finalSize);
        }

        private static UVSize GetChildSize(Orientation orientation, UIElement child, UVSize fixedChildSize) {
            var childSize = new UVSize(orientation, child.DesiredSize);
            if (!double.IsNaN(fixedChildSize.U)) childSize.U = fixedChildSize.U;
            if (!double.IsNaN(fixedChildSize.V)) childSize.V = fixedChildSize.V;
            return childSize;
        }

        private Size ArrangeKeepInOrder(Size finalSize) {
            var orientation = Orientation;
            var fixedChildSize = new UVSize(orientation, ItemWidth, ItemHeight);
            var children = InternalChildren;
            var firstInLine = 0;
            var uLimit = new UVSize(orientation, finalSize).U;
            var currentLineSize = new UVSize(orientation);
            var accumulatedV = 0d;

            for (int i = 0, count = children.Count; i < count; i++) {
                var child = children[i];
                if (child == null) continue;

                var childSize = GetChildSize(orientation, child, fixedChildSize);
                if (currentLineSize.U + childSize.U > uLimit) {
                    // Need to switch to another line
                    if (!double.IsNaN(fixedChildSize.U)) {
                        ArrangeLineFixedSize(orientation, children, accumulatedV, currentLineSize.V, firstInLine, i, fixedChildSize.U);
                    } else if (!StretchToFill) {
                        ArrangeLineDefault(orientation, children, accumulatedV, currentLineSize.V, firstInLine, i);
                    } else {
                        ArrangeLineStretch(orientation, children, accumulatedV, currentLineSize.V, firstInLine, i, uLimit, StretchProportionally);
                    }

                    accumulatedV += currentLineSize.V;
                    currentLineSize = childSize;
                    firstInLine = i;
                } else {
                    // Continue to accumulate a line
                    currentLineSize.U += childSize.U;
                    currentLineSize.V = Math.Max(childSize.V, currentLineSize.V);
                }
            }

            // Arrange the last line, if any
            if (!double.IsNaN(fixedChildSize.U)) {
                ArrangeLineFixedSize(orientation, children, accumulatedV, currentLineSize.V, firstInLine, children.Count, fixedChildSize.U);
            } else if (!StretchToFill) {
                ArrangeLineDefault(orientation, children, accumulatedV, currentLineSize.V, firstInLine, children.Count);
            } else {
                ArrangeLineStretch(orientation, children, accumulatedV, currentLineSize.V, firstInLine, children.Count, uLimit, StretchProportionally);
            }

            return finalSize;
        }

        private static void ArrangeLineDefault(Orientation orientation, UIElementCollection children, double v, double lineV, int start, int end) {
            var position = new UVSize(orientation){ U = 0d, V = v };
            for (var i = start; i < end; i++) {
                var child = children[i];
                if (child != null) {
                    var childSize = new UVSize(orientation, child.DesiredSize) { V = lineV };
                    child.Arrange(new Rect(position.Width, position.Height, childSize.Width, childSize.Height));
                    position.U += childSize.U;
                }
            }
        }

        private static void ArrangeLineStretch(Orientation orientation, UIElementCollection children, double v, double lineV, int start, int end,
                double limitU, bool stretchProportionally) {
            var totalU = 0d;
            for (var i = start; i < end; i++) {
                totalU += new UVSize(orientation, children[i].DesiredSize).U;
            }

            var position = new UVSize(orientation) { U = 0d, V = v };
            var uExtra = stretchProportionally ? limitU / totalU : (limitU - totalU) / (end - start);
            for (var i = start; i < end; i++) {
                var child = children[i];
                if (child != null) {
                    var childSize = new UVSize(orientation, child.DesiredSize) { V = lineV };
                    childSize.U = stretchProportionally ? childSize.U * uExtra : Math.Max(childSize.U + uExtra, 1d);
                    child.Arrange(new Rect(position.Width, position.Height, childSize.Width, childSize.Height));
                    position.U += childSize.U;
                }
            }
        }

        private static void ArrangeLineFixedSize(Orientation orientation, UIElementCollection children, double v, double lineV, int start, int end, double itemU) {
            var position = new UVSize(orientation) { U = 0d, V = v };
            var childSize = new UVSize(orientation){ U = itemU, V = lineV };
            for (var i = start; i < end; i++) {
                var child = children[i];
                if (child != null) {
                    child.Arrange(new Rect(position.Width, position.Height, childSize.Width, childSize.Height));
                    position.U += childSize.U;
                }
            }
        }

        private class ArrangeBestFitLine {
            public UVSize Size;
            public readonly List<int> ItemIndices = new List<int>();

            public void ArrangeDefault(Orientation orientation, UIElementCollection children, double v) {
                var position = new UVSize(orientation){ U = 0d, V = v };
                for (var i = 0; i < ItemIndices.Count; i++) {
                    var child = children[ItemIndices[i]];
                    if (child != null) {
                        var childSize = new UVSize(orientation, child.DesiredSize) { V = Size.V };
                        child.Arrange(new Rect(position.Width, position.Height, childSize.Width, childSize.Height));
                        position.U += childSize.U;
                    }
                }
            }

            public void ArrangeStretch(Orientation orientation, UIElementCollection children, double v, double limitU, bool stretchProportionally) {
                var totalU = 0d;
                for (var i = 0; i < ItemIndices.Count; i++) {
                    totalU += new UVSize(orientation, children[ItemIndices[i]].DesiredSize).U;
                }

                var position = new UVSize(orientation) { U = 0d, V = v };
                var uExtra = stretchProportionally ? limitU / totalU : (limitU - totalU) / ItemIndices.Count;
                for (var i = 0; i < ItemIndices.Count; i++) {
                    var child = children[ItemIndices[i]];
                    if (child != null) {
                        var childSize = new UVSize(orientation, child.DesiredSize) { V = Size.V };
                        childSize.U = stretchProportionally ? childSize.U * uExtra : Math.Max(childSize.U + uExtra, 1d);
                        child.Arrange(new Rect(position.Width, position.Height, childSize.Width, childSize.Height));
                        position.U += childSize.U;
                    }
                }
            }

            public void ArrangeFixedSize(Orientation orientation, UIElementCollection children, double v, double itemU) {
                var position = new UVSize(orientation) { U = 0d, V = v };
                var childSize = new UVSize(orientation){ U = itemU, V = Size.V };
                for (var i = 0; i < ItemIndices.Count; i++) {
                    var child = children[ItemIndices[i]];
                    if (child != null) {
                        child.Arrange(new Rect(position.Width, position.Height, childSize.Width, childSize.Height));
                        position.U += childSize.U;
                    }
                }
            }
        }

        private Size ArrangeBestFit(Size finalSize) {
            var orientation = Orientation;
            var fixedChildSize = new UVSize(orientation, ItemWidth, ItemHeight);
            var uLimit = new UVSize(orientation, finalSize).U;

            // First-Fit Decreasing Height (FFDH) algorithm
            var lines = new List<ArrangeBestFitLine>();
            var children = InternalChildren;
            for (int i = 0, count = children.Count; i < count; i++) {
                var child = children[i];
                if (child == null) continue;

                var childSize = GetChildSize(orientation, child, fixedChildSize);
                for (var j = 0; j < lines.Count; j++) {
                    var line = lines[j];
                    if (line.Size.U + childSize.U <= uLimit) {
                        line.Size.U += childSize.U;
                        line.Size.V = Math.Max(childSize.V, line.Size.V);
                        line.ItemIndices.Add(i);
                        goto Next;
                    }
                }

                lines.Add(new ArrangeBestFitLine {
                    Size = childSize,
                    ItemIndices = { i }
                });

                Next:
                { }
            }

            var accumulatedV = 0d;
            for (var i = 0; i < lines.Count; i++) {
                var line = lines[i];

                if (!double.IsNaN(fixedChildSize.U)) {
                    line.ArrangeFixedSize(orientation, children, accumulatedV, fixedChildSize.U);
                } else if (!StretchToFill) {
                    line.ArrangeDefault(orientation, children, accumulatedV);
                } else {
                    line.ArrangeStretch(orientation, children, accumulatedV, uLimit, StretchProportionally);
                }

                accumulatedV += line.Size.V;
            }

            return finalSize;
        }
    }
}