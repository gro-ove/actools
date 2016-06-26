using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class AlignableWrapPanel : Panel {
        public AlignableWrapPanel() {
            _orientation = Orientation.Horizontal;
        }

        private static bool IsWidthHeightValid(object value) {
            var v = (double)value;
            return double.IsNaN(v) || (v >= 0.0d && !double.IsPositiveInfinity(v));
        }

        public static readonly DependencyProperty ItemWidthProperty = DependencyProperty.Register("ItemWidth", typeof(double),
                typeof(AlignableWrapPanel), new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure),
                IsWidthHeightValid);
        
        [TypeConverter(typeof(LengthConverter))]
        public double ItemWidth {
            get { return (double)GetValue(ItemWidthProperty); }
            set { SetValue(ItemWidthProperty, value); }
        }

        public static readonly DependencyProperty ItemHeightProperty = DependencyProperty.Register("ItemHeight", typeof(double),
                typeof(AlignableWrapPanel), new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsMeasure),
                IsWidthHeightValid);
        
        [TypeConverter(typeof(LengthConverter))]
        public double ItemHeight {
            get { return (double)GetValue(ItemHeightProperty); }
            set { SetValue(ItemHeightProperty, value); }
        }

        public static readonly DependencyProperty OrientationProperty = StackPanel.OrientationProperty.AddOwner(typeof(AlignableWrapPanel),
                new FrameworkPropertyMetadata(Orientation.Horizontal, FrameworkPropertyMetadataOptions.AffectsMeasure, OnOrientationChanged));
        
        public Orientation Orientation {
            get { return _orientation; }
            set { SetValue(OrientationProperty, value); }
        }
        
        private static void OnOrientationChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            var p = (AlignableWrapPanel)d;
            p._orientation = (Orientation)e.NewValue;
        }

        private Orientation _orientation;

        private struct UvSize {
            internal UvSize(Orientation orientation, double width, double height) {
                U = V = 0d;
                _orientation = orientation;
                Width = width;
                Height = height;
            }

            internal UvSize(Orientation orientation) {
                U = V = 0d;
                _orientation = orientation;
            }

            internal double U;
            internal double V;
            private readonly Orientation _orientation;

            internal double Width {
                get { return _orientation == Orientation.Horizontal ? U : V; }
                private set { if (_orientation == Orientation.Horizontal) U = value; else V = value; }
            }

            internal double Height {
                get { return _orientation == Orientation.Horizontal ? V : U; }
                private set { if (_orientation == Orientation.Horizontal) V = value; else U = value; }
            }
        }

        protected override Size MeasureOverride(Size constraint) {
            var curLineSize = new UvSize(Orientation);
            var panelSize = new UvSize(Orientation);
            var uvConstraint = new UvSize(Orientation, constraint.Width, constraint.Height);
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

                //Flow passes its own constrint to children 
                child.Measure(childConstraint);

                //this is the size of the child in UV space 
                var sz = new UvSize(
                        Orientation,
                        itemWidthSet ? itemWidth : child.DesiredSize.Width,
                        itemHeightSet ? itemHeight : child.DesiredSize.Height);

                if (curLineSize.U + sz.U > uvConstraint.U) {
                    //need to switch to another line 
                    panelSize.U = Math.Max(curLineSize.U, panelSize.U);
                    panelSize.V += curLineSize.V;
                    curLineSize = sz;

                    if (!(sz.U > uvConstraint.U)) continue;
                    //the element is wider then the constrint - give it a separate line
                    panelSize.U = Math.Max(sz.U, panelSize.U);
                    panelSize.V += sz.V;
                    curLineSize = new UvSize(Orientation);
                } else {
                    //continue to accumulate a line
                    curLineSize.U += sz.U;
                    curLineSize.V = Math.Max(sz.V, curLineSize.V);
                }
            }

            //the last line size, if any should be added 
            panelSize.U = Math.Max(curLineSize.U, panelSize.U);
            panelSize.V += curLineSize.V;

            //go from UV space to W/H space
            return new Size(panelSize.Width, panelSize.Height);
        }
        
        protected override Size ArrangeOverride(Size finalSize) {
            var firstInLine = 0;
            var itemWidth = ItemWidth;
            var itemHeight = ItemHeight;
            double accumulatedV = 0;
            var itemU = Orientation == Orientation.Horizontal ? itemWidth : itemHeight;
            var curLineSize = new UvSize(Orientation);
            var uvFinalSize = new UvSize(Orientation, finalSize.Width, finalSize.Height);
            var itemWidthSet = !double.IsNaN(itemWidth);
            var itemHeightSet = !double.IsNaN(itemHeight);
            var useItemU = Orientation == Orientation.Horizontal ? itemWidthSet : itemHeightSet;

            var children = InternalChildren;

            for (int i = 0, count = children.Count; i < count; i++) {
                var child = children[i];
                if (child == null) continue;

                var sz = new UvSize(
                        Orientation,
                        itemWidthSet ? itemWidth : child.DesiredSize.Width,
                        itemHeightSet ? itemHeight : child.DesiredSize.Height);

                if (curLineSize.U + sz.U > uvFinalSize.U) {
                    //need to switch to another line 
                    ArrangeLine(finalSize, accumulatedV, curLineSize, firstInLine, i, useItemU, itemU);

                    accumulatedV += curLineSize.V;
                    curLineSize = sz;

                    if (sz.U > uvFinalSize.U) {
                        //the element is wider then the constraint - give it a separate line 
                        //switch to next line which only contain one element 
                        ArrangeLine(finalSize, accumulatedV, sz, i, ++i, useItemU, itemU);

                        accumulatedV += sz.V;
                        curLineSize = new UvSize(Orientation);
                    }

                    firstInLine = i;
                } else {
                    //continue to accumulate a line
                    curLineSize.U += sz.U;
                    curLineSize.V = Math.Max(sz.V, curLineSize.V);
                }
            }

            //arrange the last line, if any
            if (firstInLine < children.Count) {
                ArrangeLine(finalSize, accumulatedV, curLineSize, firstInLine, children.Count, useItemU, itemU);
            }

            return finalSize;
        }

        private void ArrangeLine(Size finalSize, double v, UvSize line, int start, int end, bool useItemU, double itemU) {
            double u;
            var isHorizontal = Orientation == Orientation.Horizontal;

            if (_orientation == Orientation.Vertical) {
                switch (VerticalContentAlignment) {
                    case VerticalAlignment.Center:
                        u = (finalSize.Height - line.U) / 2;
                        break;
                    case VerticalAlignment.Bottom:
                        u = finalSize.Height - line.U;
                        break;
                    default:
                        u = 0;
                        break;
                }
            } else {
                switch (HorizontalContentAlignment) {
                    case HorizontalAlignment.Center:
                        u = (finalSize.Width - line.U) / 2;
                        break;
                    case HorizontalAlignment.Right:
                        u = finalSize.Width - line.U;
                        break;
                    default:
                        u = 0;
                        break;
                }
            }

            var children = InternalChildren;
            for (var i = start; i < end; i++) {
                var child = children[i];
                if (child == null) continue;
                var childSize = new UvSize(Orientation, child.DesiredSize.Width, child.DesiredSize.Height);
                var layoutSlotU = useItemU ? itemU : childSize.U;
                child.Arrange(new Rect(
                        isHorizontal ? u : v,
                        isHorizontal ? v : u,
                        isHorizontal ? layoutSlotU : line.V,
                        isHorizontal ? line.V : layoutSlotU));
                u += layoutSlotU;
            }
        }

        public static readonly DependencyProperty HorizontalContentAlignmentProperty = DependencyProperty.Register(nameof(HorizontalContentAlignment), typeof(HorizontalAlignment),
                typeof(AlignableWrapPanel), new FrameworkPropertyMetadata(HorizontalAlignment.Left, FrameworkPropertyMetadataOptions.AffectsArrange));

        public HorizontalAlignment HorizontalContentAlignment {
            get { return (HorizontalAlignment)GetValue(HorizontalContentAlignmentProperty); }
            set { SetValue(HorizontalContentAlignmentProperty, value); }
        }

        public static readonly DependencyProperty VerticalContentAlignmentProperty = DependencyProperty.Register(nameof(VerticalContentAlignment), typeof(VerticalAlignment),
                typeof(AlignableWrapPanel), new FrameworkPropertyMetadata(VerticalAlignment.Top, FrameworkPropertyMetadataOptions.AffectsArrange));

        public VerticalAlignment VerticalContentAlignment {
            get { return (VerticalAlignment)GetValue(VerticalContentAlignmentProperty); }
            set { SetValue(VerticalContentAlignmentProperty, value); }
        }
    }
}
