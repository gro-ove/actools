using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class VirtualizingTilePanel : VirtualizingPanel, IScrollInfo {
        private const double ScrollLineAmount = 16.0;

        private Size _extentSize;
        private Size _viewportSize;
        private Point _offset;
        private ItemsControl _itemsControl;
        private readonly Dictionary<UIElement, Rect> _childLayouts = new Dictionary<UIElement, Rect>();

        public static readonly DependencyProperty OrientationProperty = DependencyProperty.Register(nameof(Orientation), typeof(Orientation),
                typeof(VirtualizingTilePanel), new PropertyMetadata(Orientation.Horizontal));

        public Orientation Orientation {
            get { return (Orientation)GetValue(OrientationProperty); }
            set { SetValue(OrientationProperty, value); }
        }

        public static readonly DependencyProperty ItemWidthProperty =
                DependencyProperty.Register("ItemWidth", typeof(double), typeof(VirtualizingTilePanel), new PropertyMetadata(1.0, HandleItemDimensionChanged));

        public static readonly DependencyProperty ItemHeightProperty =
                DependencyProperty.Register("ItemHeight", typeof(double), typeof(VirtualizingTilePanel), new PropertyMetadata(1.0, HandleItemDimensionChanged));

        private static readonly DependencyProperty VirtualItemIndexProperty =
                DependencyProperty.RegisterAttached("VirtualItemIndex", typeof(int), typeof(VirtualizingTilePanel), new PropertyMetadata(-1));
        private IRecyclingItemContainerGenerator _itemsGenerator;

        private bool _isInMeasure;

        private static int GetVirtualItemIndex(DependencyObject obj) {
            return (int)obj.GetValue(VirtualItemIndexProperty);
        }

        private static void SetVirtualItemIndex(DependencyObject obj, int value) {
            obj.SetValue(VirtualItemIndexProperty, value);
        }

        public double ItemHeight {
            get { return (double)GetValue(ItemHeightProperty); }
            set { SetValue(ItemHeightProperty, value); }
        }

        public double ItemWidth {
            get { return (double)GetValue(ItemWidthProperty); }
            set { SetValue(ItemWidthProperty, value); }
        }

        public VirtualizingTilePanel() {
            if (!DesignerProperties.GetIsInDesignMode(this)) {
                Dispatcher.BeginInvoke((Action)Initialize);
            }
        }

        private void Initialize() {
            _itemsControl = ItemsControl.GetItemsOwner(this);
            _itemsGenerator = (IRecyclingItemContainerGenerator)ItemContainerGenerator;
            InvalidateMeasure();
        }

        protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args) {
            base.OnItemsChanged(sender, args);
            InvalidateMeasure();
        }

        protected override Size MeasureOverride(Size availableSize) {
            if (_itemsControl == null) {
                return new Size(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width,
                        double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height);
            }

            _isInMeasure = true;
            _childLayouts.Clear();

            var extentInfo = GetExtentInfo(availableSize);
            EnsureScrollOffsetIsWithinConstrains(extentInfo);

            var layoutInfo = GetLayoutInfo(availableSize, ItemWidth, ItemHeight, extentInfo);
            RecycleItems(layoutInfo);

            // Determine where the first item is in relation to previously realized items
            var generatorStartPosition = _itemsGenerator.GeneratorPositionFromIndex(layoutInfo.FirstRealizedItemIndex);
            var visualIndex = 0;

            var currentX = layoutInfo.FirstRealizedItemLeft;
            var currentY = layoutInfo.FirstRealizedItemTop;

            var orientation = Orientation;
            double offsetX, offsetY;
            switch (HorizontalContentAlignment) {
                case HorizontalAlignment.Left:
                case HorizontalAlignment.Stretch:
                    offsetX = 0d;
                    break;
                case HorizontalAlignment.Center:
                    if (orientation == Orientation.Horizontal) {
                        offsetX = (availableSize.Width - extentInfo.ItemsPerRow * ItemWidth) / 2d;
                    } else {
                        offsetX = Math.Max(availableSize.Width - extentInfo.TotalRows * ItemWidth, 0d) / 2d;
                    }
                    break;
                case HorizontalAlignment.Right:
                    if (orientation == Orientation.Horizontal) {
                        offsetX = availableSize.Width - extentInfo.ItemsPerRow * ItemWidth;
                    } else {
                        offsetX = Math.Max(availableSize.Width - extentInfo.TotalRows * ItemWidth, 0d);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            switch (VerticalContentAlignment) {
                case VerticalAlignment.Top:
                case VerticalAlignment.Stretch:
                    offsetY = 0d;
                    break;
                case VerticalAlignment.Center:
                    if (orientation == Orientation.Horizontal) {
                        offsetY = Math.Max(availableSize.Height - extentInfo.TotalRows * ItemHeight, 0d) / 2d;
                    } else {
                        offsetY = (availableSize.Height - extentInfo.ItemsPerRow * ItemHeight) / 2d;
                    }
                    break;
                case VerticalAlignment.Bottom:
                    if (orientation == Orientation.Horizontal) {
                        offsetY = Math.Max(availableSize.Height - extentInfo.TotalRows * ItemHeight, 0d);
                    } else {
                        offsetY = availableSize.Height - extentInfo.ItemsPerRow * ItemHeight;
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            using (_itemsGenerator.StartAt(generatorStartPosition, GeneratorDirection.Forward, true)) {
                for (var itemIndex = layoutInfo.FirstRealizedItemIndex; itemIndex <= layoutInfo.LastRealizedItemIndex; itemIndex++, visualIndex++) {
                    bool newlyRealized;
                    var child = (UIElement)_itemsGenerator.GenerateNext(out newlyRealized);
                    SetVirtualItemIndex(child, itemIndex);

                    if (newlyRealized) {
                        InsertInternalChild(visualIndex, child);
                    } else {
                        // check if item needs to be moved into a new position in the Children collection
                        if (visualIndex < Children.Count) {
                            if (!ReferenceEquals(Children[visualIndex], child)) {
                                var childCurrentIndex = Children.IndexOf(child);

                                if (childCurrentIndex >= 0) {
                                    RemoveInternalChildRange(childCurrentIndex, 1);
                                }

                                InsertInternalChild(visualIndex, child);
                            }
                        } else {
                            // we know that the child can't already be in the children collection
                            // because we've been inserting children in correct visualIndex order,
                            // and this child has a visualIndex greater than the Children.Count
                            AddInternalChild(child);
                        }
                    }

                    // only prepare the item once it has been added to the visual tree
                    _itemsGenerator.PrepareItemContainer(child);

                    child.Measure(new Size(ItemWidth, ItemHeight));
                    _childLayouts.Add(child, new Rect(currentX + offsetX, currentY + offsetY, ItemWidth, ItemHeight));

                    if (orientation == Orientation.Horizontal) {
                        if (currentX + ItemWidth * 2 >= availableSize.Width) {
                            // wrap to a new line
                            currentY += ItemHeight;
                            currentX = 0;
                        } else {
                            currentX += ItemWidth;
                        }
                    } else {
                        if (currentY + ItemHeight * 2 >= availableSize.Height) {
                            // wrap to a new column
                            currentX += ItemWidth;
                            currentY = 0;
                        } else {
                            currentY += ItemHeight;
                        }
                    }
                }
            }

            RemoveRedundantChildren();
            UpdateScrollInfo(availableSize, extentInfo);

            var desiredSize = new Size(double.IsInfinity(availableSize.Width) ? 0 : availableSize.Width, double.IsInfinity(availableSize.Height) ? 0 : availableSize.Height);
            _isInMeasure = false;

            return desiredSize;
        }

        private void EnsureScrollOffsetIsWithinConstrains(ExtentInfo extentInfo) {
            if (Orientation == Orientation.Horizontal) {
                _offset.Y = Clamp(_offset.Y, 0, extentInfo.MaxScrollOffset);
            } else {
                _offset.X = Clamp(_offset.X, 0, extentInfo.MaxScrollOffset);
            }
        }

        private void RecycleItems(ItemLayoutInfo layoutInfo) {
            foreach (UIElement child in Children) {
                var virtualItemIndex = GetVirtualItemIndex(child);

                if (virtualItemIndex < layoutInfo.FirstRealizedItemIndex || virtualItemIndex > layoutInfo.LastRealizedItemIndex) {
                    var generatorPosition = _itemsGenerator.GeneratorPositionFromIndex(virtualItemIndex);
                    if (generatorPosition.Index >= 0) {
                        _itemsGenerator.Recycle(generatorPosition, 1);
                    }
                }

                SetVirtualItemIndex(child, -1);
            }
        }

        protected override Size ArrangeOverride(Size finalSize) {
            foreach (UIElement child in Children) {
                child.Arrange(_childLayouts[child]);
            }
            return finalSize;
        }

        private void UpdateScrollInfo(Size availableSize, ExtentInfo extentInfo) {
            _viewportSize = availableSize;
            _extentSize = Orientation == Orientation.Horizontal ? new Size(availableSize.Width, extentInfo.ExtentSize) : new Size(extentInfo.ExtentSize, availableSize.Height);
            InvalidateScrollInfo();
        }

        private void RemoveRedundantChildren() {
            // iterate backwards through the child collection because we’re going to be
            // removing items from it
            for (var i = Children.Count - 1; i >= 0; i--) {
                var child = Children[i];

                // if the virtual item index is -1, this indicates
                // it is a recycled item that hasn't been reused this time round
                if (GetVirtualItemIndex(child) == -1) {
                    RemoveInternalChildRange(i, 1);
                }
            }
        }

        private ItemLayoutInfo GetLayoutInfo(Size availableSize, double itemWidth, double itemHeight, ExtentInfo extentInfo) {
            if (_itemsControl == null) {
                return new ItemLayoutInfo();
            }

            // we need to ensure that there is one realized item prior to the first visible item, and one after the last visible item,
            // so that keyboard navigation works properly. For example, when focus is on the first visible item, and the user
            // navigates up, the ListBox selects the previous item, and the scrolls that into view - and this triggers the loading of the rest of the items 
            // in that row

            if (Orientation == Orientation.Horizontal) {
                var firstVisibleLine = (int)Math.Floor(VerticalOffset / itemHeight);

                var firstRealizedIndex = Math.Max(extentInfo.ItemsPerRow * firstVisibleLine - 1, 0);
                var firstRealizedItemLeft = firstRealizedIndex % extentInfo.ItemsPerRow * itemWidth - HorizontalOffset;
                var firstRealizedItemTop = Math.Floor((double)firstRealizedIndex / extentInfo.ItemsPerRow) * itemHeight - VerticalOffset;

                var firstCompleteLineTop = (firstVisibleLine == 0 ? firstRealizedItemTop : firstRealizedItemTop + itemHeight);
                var completeRealizedLines = (int)Math.Ceiling((availableSize.Height - firstCompleteLineTop) / itemHeight);

                var lastRealizedIndex = Math.Min(firstRealizedIndex + completeRealizedLines * extentInfo.ItemsPerRow + 2, _itemsControl.Items.Count - 1);
                return new ItemLayoutInfo(firstRealizedIndex, firstRealizedItemTop, firstRealizedItemLeft, lastRealizedIndex);
            } else {
                var firstVisibleColumn = (int)Math.Floor(HorizontalOffset / itemWidth);

                var firstRealizedIndex = Math.Max(extentInfo.ItemsPerRow * firstVisibleColumn - 1, 0);
                var firstRealizedItemTop = firstRealizedIndex % extentInfo.ItemsPerRow * itemHeight - VerticalOffset;
                var firstRealizedItemLeft = Math.Floor((double)firstRealizedIndex / extentInfo.ItemsPerRow) * itemWidth - HorizontalOffset;

                var firstCompleteColumnLeft = (firstVisibleColumn == 0 ? firstRealizedItemLeft : firstRealizedItemLeft + itemWidth);
                var completeRealizedColumns = (int)Math.Ceiling((availableSize.Width - firstCompleteColumnLeft) / itemWidth);

                var lastRealizedIndex = Math.Min(firstRealizedIndex + completeRealizedColumns * extentInfo.ItemsPerRow + 2, _itemsControl.Items.Count - 1);
                return new ItemLayoutInfo(firstRealizedIndex, firstRealizedItemTop, firstRealizedItemLeft, lastRealizedIndex);
            }
        }

        private ExtentInfo GetExtentInfo(Size viewPortSize) {
            if (_itemsControl == null) {
                return new ExtentInfo();
            }

            if (Orientation == Orientation.Horizontal) {
                var itemsPerLine = Math.Max((int)Math.Floor(viewPortSize.Width / ItemWidth), 1);
                var totalLines = (int)Math.Ceiling((double)_itemsControl.Items.Count / itemsPerLine);
                var extentHeight = Math.Max(totalLines * ItemHeight, viewPortSize.Height);
                return new ExtentInfo(itemsPerLine, totalLines, extentHeight, extentHeight - viewPortSize.Height);
            } else {
                var itemsPerColumn = Math.Max((int)Math.Floor(viewPortSize.Height / ItemHeight), 1);
                var totalColumns = (int)Math.Ceiling((double)_itemsControl.Items.Count / itemsPerColumn);
                var extentWidth = Math.Max(totalColumns * ItemWidth, viewPortSize.Width);
                return new ExtentInfo(itemsPerColumn, totalColumns, extentWidth, extentWidth - viewPortSize.Width);
            }
        }

        public void LineUp() {
            SetVerticalOffset(VerticalOffset - ScrollLineAmount);
        }

        public void LineDown() {
            SetVerticalOffset(VerticalOffset + ScrollLineAmount);
        }

        public void LineLeft() {
            SetHorizontalOffset(HorizontalOffset + ScrollLineAmount);
        }

        public void LineRight() {
            SetHorizontalOffset(HorizontalOffset - ScrollLineAmount);
        }

        public void PageUp() {
            SetVerticalOffset(VerticalOffset - ViewportHeight);
        }

        public void PageDown() {
            SetVerticalOffset(VerticalOffset + ViewportHeight);
        }

        public void PageLeft() {
            SetHorizontalOffset(HorizontalOffset + ItemWidth);
        }

        public void PageRight() {
            SetHorizontalOffset(HorizontalOffset - ItemWidth);
        }

        public void MouseWheelUp() {
            SetVerticalOffset(VerticalOffset - ScrollLineAmount * SystemParameters.WheelScrollLines);
        }

        public void MouseWheelDown() {
            SetVerticalOffset(VerticalOffset + ScrollLineAmount * SystemParameters.WheelScrollLines);
        }

        public void MouseWheelLeft() {
            SetHorizontalOffset(HorizontalOffset - ScrollLineAmount * SystemParameters.WheelScrollLines);
        }

        public void MouseWheelRight() {
            SetHorizontalOffset(HorizontalOffset + ScrollLineAmount * SystemParameters.WheelScrollLines);
        }

        public void SetHorizontalOffset(double offset) {
            if (_isInMeasure) {
                return;
            }

            _offset = new Point(Clamp(offset, 0, ExtentWidth - ViewportWidth), _offset.Y);
            InvalidateScrollInfo();
            InvalidateMeasure();
        }

        public void SetVerticalOffset(double offset) {
            if (_isInMeasure) {
                return;
            }

            _offset = new Point(_offset.X, Clamp(offset, 0, ExtentHeight - ViewportHeight));
            InvalidateScrollInfo();
            InvalidateMeasure();
        }

        public Rect MakeVisible(Visual visual, Rect rectangle) {
            if (rectangle.IsEmpty || visual == null || ReferenceEquals(visual, this) || !IsAncestorOf(visual)) {
                return Rect.Empty;
            }

            rectangle = visual.TransformToAncestor(this).TransformBounds(rectangle);

            var viewRect = new Rect(HorizontalOffset, VerticalOffset, ViewportWidth, ViewportHeight);
            rectangle.X += viewRect.X;
            rectangle.Y += viewRect.Y;

            viewRect.X = CalculateNewScrollOffset(viewRect.Left, viewRect.Right, rectangle.Left, rectangle.Right);
            viewRect.Y = CalculateNewScrollOffset(viewRect.Top, viewRect.Bottom, rectangle.Top, rectangle.Bottom);

            SetHorizontalOffset(viewRect.X);
            SetVerticalOffset(viewRect.Y);
            rectangle.Intersect(viewRect);

            rectangle.X -= viewRect.X;
            rectangle.Y -= viewRect.Y;

            return rectangle;
        }

        private static double CalculateNewScrollOffset(double topView, double bottomView, double topChild, double bottomChild) {
            var offBottom = topChild < topView && bottomChild < bottomView;
            var offTop = bottomChild > bottomView && topChild > topView;
            var tooLarge = (bottomChild - topChild) > (bottomView - topView);
            return offBottom || offTop ? (offBottom && !tooLarge || offTop && tooLarge ? topChild : bottomChild - (bottomView - topView)) : topView;
        }


        public ItemLayoutInfo GetVisibleItemsRange() {
            return GetLayoutInfo(_viewportSize, ItemWidth, ItemHeight, GetExtentInfo(_viewportSize));
        }

        public bool CanVerticallyScroll { get; set; }

        public bool CanHorizontallyScroll { get; set; }

        public double ExtentWidth => _extentSize.Width;

        public double ExtentHeight => _extentSize.Height;

        public double ViewportWidth => _viewportSize.Width;

        public double ViewportHeight => _viewportSize.Height;

        public double HorizontalOffset => _offset.X;

        public double VerticalOffset => _offset.Y;

        public ScrollViewer ScrollOwner { get; set; }

        private void InvalidateScrollInfo() {
            ScrollOwner?.InvalidateScrollInfo();
        }

        private static void HandleItemDimensionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) {
            (d as VirtualizingTilePanel)?.InvalidateMeasure();
        }

        private static double Clamp(double value, double min, double max) {
            return Math.Min(Math.Max(value, min), max);
        }

        internal struct ExtentInfo {
            /// <summary>
            /// Per column or line, depends on orientation.
            /// </summary>
            public int ItemsPerRow;

            /// <summary>
            /// Columns or lines, depends on orientation.
            /// </summary>
            public int TotalRows;

            /// <summary>
            /// Height or width, depends on orientation.
            /// </summary>
            public double ExtentSize;

            /// <summary>
            /// Vertical or horizontal offset, depends on orientation.
            /// </summary>
            public double MaxScrollOffset;

            public ExtentInfo(int itemsPerRow, int totalRows, double extentSize, double maxScrollOffset) {
                ItemsPerRow = itemsPerRow;
                TotalRows = totalRows;
                ExtentSize = extentSize;
                MaxScrollOffset = maxScrollOffset;
            }
        }

        public struct ItemLayoutInfo {
            public int FirstRealizedItemIndex;
            public double FirstRealizedItemTop;
            public double FirstRealizedItemLeft;
            public int LastRealizedItemIndex;

            public ItemLayoutInfo(int firstRealizedItemIndex, double firstRealizedItemTop, double firstRealizedItemLeft, int lastRealizedItemIndex) {
                FirstRealizedItemIndex = firstRealizedItemIndex;
                FirstRealizedItemTop = firstRealizedItemTop;
                FirstRealizedItemLeft = firstRealizedItemLeft;
                LastRealizedItemIndex = lastRealizedItemIndex;
            }
        }

        public static readonly DependencyProperty HorizontalContentAlignmentProperty = DependencyProperty.Register(nameof(HorizontalContentAlignment), typeof(HorizontalAlignment), typeof(VirtualizingTilePanel), new FrameworkPropertyMetadata(HorizontalAlignment.Left, FrameworkPropertyMetadataOptions.AffectsArrange));

        public HorizontalAlignment HorizontalContentAlignment {
            get { return (HorizontalAlignment)GetValue(HorizontalContentAlignmentProperty); }
            set { SetValue(HorizontalContentAlignmentProperty, value); }
        }

        public static readonly DependencyProperty VerticalContentAlignmentProperty = DependencyProperty.Register(nameof(VerticalContentAlignment), typeof(VerticalAlignment), typeof(VirtualizingTilePanel), new FrameworkPropertyMetadata(VerticalAlignment.Top, FrameworkPropertyMetadataOptions.AffectsArrange));

        public VerticalAlignment VerticalContentAlignment {
            get { return (VerticalAlignment)GetValue(VerticalContentAlignmentProperty); }
            set { SetValue(VerticalContentAlignmentProperty, value); }
        }
    }
}