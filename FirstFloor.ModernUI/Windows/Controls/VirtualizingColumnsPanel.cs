using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class VirtualizingColumnsPanel : VirtualizingPanel, IScrollInfo {
        public static readonly DependencyProperty ColumnsProperty = DependencyProperty.Register(nameof(Columns), typeof(int),
                typeof(VirtualizingColumnsPanel), new PropertyMetadata(2, (o, e) => {
                    var panel = (VirtualizingColumnsPanel)o;
                    panel._columns = (int)e.NewValue;
                    OnItemsSourceChanged(panel);
                }));

        private int _columns = 2;

        public int Columns {
            get => _columns;
            set => SetValue(ColumnsProperty, value);
        }

        public static readonly DependencyProperty ItemHeightModifierProperty = DependencyProperty.Register(nameof(ItemHeightModifier), typeof(double),
                typeof(VirtualizingColumnsPanel), new PropertyMetadata(0.8, (o, e) => {
                    var panel = (VirtualizingColumnsPanel)o;
                    panel._itemHeightModifier = (double)e.NewValue;
                    OnItemsSourceChanged(panel);
                }));

        private double _itemHeightModifier = 0.8;

        public double ItemHeightModifier {
            get => _itemHeightModifier;
            set => SetValue(ItemHeightModifierProperty, value);
        }

        private double ItemWidth { get; set; }
        private double ItemHeight => ItemWidth * ItemHeightModifier;

        private static void OnItemsSourceChanged(VirtualizingColumnsPanel panel) {
            if (panel._itemsControl == null) return;
            panel.InvalidateMeasure();
            panel.ScrollOwner?.InvalidateScrollInfo();
            panel.SetVerticalOffset(0);
        }

        private IRecyclingItemContainerGenerator _generator;
        private ItemContainerGenerator GeneratorContainer => (ItemContainerGenerator)_generator;
        private ItemsControl _itemsControl;

        public VirtualizingColumnsPanel() {
            if (!DesignerProperties.GetIsInDesignMode(this)) {
                Dispatcher.BeginInvoke((Action)delegate {
                    _itemsControl = ItemsControl.GetItemsOwner(this);
                    _generator = (IRecyclingItemContainerGenerator)ItemContainerGenerator;
                    InvalidateMeasure();
                });
            }

            RenderTransform = _translate;
        }

        protected override Size MeasureOverride(Size availableSize) {
            if (_itemsControl == null) {
                return availableSize.Width == double.PositiveInfinity || availableSize.Height == double.PositiveInfinity
                        ? Size.Empty : availableSize;
            }

            UpdateScrollInfo(availableSize);
            GetVisibleRange(out var firstVisibleItemIndex, out var lastVisibleItemIndex);
            CleanUpItems(lastVisibleItemIndex, firstVisibleItemIndex);
            if (lastVisibleItemIndex == -1) return availableSize;

            var children = InternalChildren;
            var startPos = _generator.GeneratorPositionFromIndex(firstVisibleItemIndex);
            var childIndex = startPos.Offset == 0 ? startPos.Index : startPos.Index + 1;
            using (_generator.StartAt(startPos, GeneratorDirection.Forward, true)) {
                for (var itemIndex = firstVisibleItemIndex; itemIndex <= lastVisibleItemIndex; ++itemIndex, ++childIndex) {
                    var child = (UIElement)_generator.GenerateNext(out var newlyRealized);
                    if (newlyRealized) {
                        if (childIndex >= children.Count) {
                            AddInternalChild(child);
                        } else {
                            InsertInternalChild(childIndex, child);
                        }
                        _generator.PrepareItemContainer(child);
                    } else if (!children.Contains(child)) {
                        InsertInternalChild(childIndex, child);
                        ItemContainerGenerator.PrepareItemContainer(child);
                    }

                    child.Measure(new Size(ItemWidth, ItemHeight));
                }
            }

            return availableSize;
        }

        private void CleanUpItems(int lastVisibleItemIndex, int firstVisibleItemIndex) {
            var cacheLength = GetCacheLength(this);
            double multiplier;
            switch (GetCacheLengthUnit(this)) {
                case VirtualizationCacheLengthUnit.Pixel:
                    multiplier = 1d / ItemHeight;
                    break;
                case VirtualizationCacheLengthUnit.Item:
                    multiplier = 1d;
                    break;
                case VirtualizationCacheLengthUnit.Page:
                    multiplier = lastVisibleItemIndex - firstVisibleItemIndex + 1;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var minDesiredGenerated = (int)(firstVisibleItemIndex - cacheLength.CacheBeforeViewport * multiplier);
            var maxDesiredGenerated = (int)(lastVisibleItemIndex + cacheLength.CacheAfterViewport * multiplier);
            var children = Children;
            for (var i = children.Count - 1; i >= 0; i--) {
                var childGeneratorPosition = new GeneratorPosition(i, 0);
                var iIndex = ItemContainerGenerator.IndexFromGeneratorPosition(childGeneratorPosition);
                if (iIndex < minDesiredGenerated || iIndex > maxDesiredGenerated) {
                    _generator.Recycle(childGeneratorPosition, 1);
                    RemoveInternalChildRange(i, 1);
                }
            }
        }

        protected override Size ArrangeOverride(Size finalSize) {
            var children = Children;
            for (var i = 0; i < children.Count; i++) {
                ArrangeChild(_generator.IndexFromGeneratorPosition(new GeneratorPosition(i, 0)), children[i]);
            }
            return finalSize;
        }

        protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args) {
            switch (args.Action) {
                case NotifyCollectionChangedAction.Remove:
                case NotifyCollectionChangedAction.Replace:
                    RemoveInternalChildRange(args.Position.Index, args.ItemUICount);
                    break;
                case NotifyCollectionChangedAction.Move:
                    RemoveInternalChildRange(args.OldPosition.Index, args.ItemUICount);
                    break;
            }
        }

        #region Layout specific code
        private void GetVisibleRange(out int firstVisibleItemIndex, out int lastVisibleItemIndex) {
            var itemCount = _itemsControl.HasItems ? _itemsControl.Items.Count : 0;
            firstVisibleItemIndex = Math.Min(Math.Max((int)Math.Floor(_offset.Y / ItemHeight) * Columns, 0), itemCount - 1);
            lastVisibleItemIndex = Math.Min(firstVisibleItemIndex + ((int)Math.Ceiling(_viewport.Height / ItemHeight) + 1) * Columns, itemCount - 1);
        }

        private static int GetItemRow(int itemIndex, int itemPerRow) {
            var column = itemIndex % itemPerRow;
            return itemIndex < column ? 0 : (int)Math.Floor(itemIndex / (double)itemPerRow);
        }

        private void ArrangeChild(int itemIndex, UIElement child) {
            var childrenPerRow = Columns;
            var column = itemIndex % childrenPerRow;
            var row = GetItemRow(itemIndex, childrenPerRow);
            var targetRect = new Rect(column * ItemWidth, ItemHeight * row, ItemWidth, ItemHeight);
            child.Arrange(targetRect);
        }
        #endregion

        #region IScrollInfo implementation
        private double GetTotalHeight() {
            var itemCount = _itemsControl.HasItems ? _itemsControl.Items.Count : 0;
            var rows = Math.Ceiling((double)itemCount / Columns);
            double totalHeight = 0;
            for (var i = 0; i < rows; i++) {
                totalHeight += ItemHeight;
            }
            return totalHeight;
        }

        private void UpdateScrollInfo(Size availableSize) {
            if (_itemsControl == null) return;

            ItemWidth = Math.Floor(availableSize.Width / Columns);

            var totalHeight = GetTotalHeight();
            if (_offset.Y > totalHeight - availableSize.Height) {
                _offset.Y = Math.Max(totalHeight - availableSize.Height, 0);
                _translate.Y = -_offset.Y;
            }

            var extent = new Size(Columns * ItemWidth, totalHeight);
            if (extent != _extent) {
                _extent = extent;
                ScrollOwner?.InvalidateScrollInfo();
            }

            if (availableSize != _viewport) {
                _viewport = availableSize;
                ScrollOwner?.InvalidateScrollInfo();
            }

            if (_scrollLater != null && _extent.Height > 0d) {
                SetVerticalOffset(_scrollLater.Value);
                _scrollLater = null;
            }
        }

        public ScrollViewer ScrollOwner { get; set; }
        public bool CanHorizontallyScroll { get; set; }
        public bool CanVerticallyScroll { get; set; }

        public double HorizontalOffset => _offset.X;
        public double VerticalOffset => _offset.Y;
        public double ExtentHeight => _extent.Height;
        public double ExtentWidth => _extent.Width;
        public double ViewportHeight => _viewport.Height;
        public double ViewportWidth => _viewport.Width;

        private const double ScrollLineAmount = 16;

        public void LineUp() {
            SetVerticalOffset(VerticalOffset - ScrollLineAmount);
        }

        public void LineDown() {
            SetVerticalOffset(VerticalOffset + ScrollLineAmount);
        }

        public void PageUp() {
            SetVerticalOffset(VerticalOffset - _viewport.Height);
        }

        public void PageDown() {
            SetVerticalOffset(VerticalOffset + _viewport.Height);
        }

        public void MouseWheelUp() {
            SetVerticalOffset(VerticalOffset - ItemWidth);
        }

        public void MouseWheelDown() {
            SetVerticalOffset(VerticalOffset + ItemWidth);
        }

        public void LineLeft() { }
        public void LineRight() { }

        public Rect MakeVisible(Visual visual, Rect rectangle) {
            var index = GeneratorContainer.IndexFromContainer(visual);
            var row = GetItemRow(index, Columns);
            var offset = ItemHeight * row;
            var offsetSize = offset + ItemHeight;
            var offsetBottom = _offset.Y + _viewport.Height;
            if (offset > _offset.Y && offsetSize < offsetBottom) {
                return rectangle;
            }

            if (offset > _offset.Y && offsetBottom - offset < ItemHeight) {
                offset = _offset.Y + (ItemHeight - (offsetBottom - offset));
            } else if (Math.Floor(offsetBottom - offset) == Math.Floor(ItemHeight)) {
                return rectangle;
            }

            _offset.Y = offset;
            _translate.Y = -offset;
            InvalidateMeasure();
            return rectangle;
        }

        public void MouseWheelLeft() { }
        public void MouseWheelRight() { }
        public void PageLeft() { }
        public void PageRight() { }
        public void SetHorizontalOffset(double offset) { }

        public void SetVerticalOffset(double offset) {
            if (_extent.Height == 0 && offset != 0) {
                _scrollLater = offset;
                return;
            }

            if (offset < 0 || _viewport.Height >= _extent.Height) {
                offset = 0;
            } else if (offset + _viewport.Height >= _extent.Height) {
                offset = _extent.Height - _viewport.Height;
            }

            _offset.Y = offset;
            ScrollOwner?.InvalidateScrollInfo();
            _translate.Y = -offset;
            InvalidateMeasure();
        }

        private readonly TranslateTransform _translate = new TranslateTransform();
        private double? _scrollLater;
        private Size _extent = new Size(0, 0);
        private Size _viewport = new Size(0, 0);
        private Point _offset;
        #endregion
    }
}