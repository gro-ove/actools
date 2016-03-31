using System;
using System.Windows;
using System.Windows.Controls;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class TextBlockAutoShrink : TextBlock {
        private double _defaultMargin = 6;

        static TextBlockAutoShrink() {
            TextProperty.OverrideMetadata(typeof(TextBlockAutoShrink), new FrameworkPropertyMetadata(TextPropertyChanged));
        }

        public TextBlockAutoShrink() {
            DataContextChanged += TextBlockAutoShrink_DataContextChanged;
        }

        private static void TextPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) {
            var s = sender as TextBlockAutoShrink;
            if (s == null) return;
            if (s._originalFontSize.HasValue) {
                s.FontSize = s._originalFontSize.Value;
            } else {
                s.FitSize();
            }
        }

        void TextBlockAutoShrink_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e) {
            FitSize();
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo) {
            FitSize();
            base.OnRenderSizeChanged(sizeInfo);
        }

        private double? _originalFontSize;

        private void FitSize() {
            var parent = Parent as FrameworkElement;
            if (parent == null) return;

            if (_originalFontSize == null) {
                _originalFontSize = FontSize;
            }

            var targetWidthSize = FontSize;
            var targetHeightSize = FontSize;

            var maxWidth = double.IsNaN(Width) ? double.IsInfinity(MaxWidth) ? parent.ActualWidth : MaxWidth : Width;
            var maxHeight = double.IsNaN(Height) ? double.IsInfinity(MaxHeight) ? parent.ActualHeight : MaxHeight : Height;


            if (ActualWidth > maxWidth) {
                targetWidthSize = FontSize * (maxWidth / (ActualWidth + _defaultMargin));
            }

            if (ActualHeight > maxHeight) {
                var ratio = maxHeight / ActualHeight;

                // Normalize due to Height miscalculation. We do it step by step repeatedly until the requested height is reached. Once the fontsize is changed, this event is re-raised
                // And the ActualHeight is lowered a bit more until it doesnt enter the enclosing If block.
                ratio = 1 - ratio > 0.04 ? Math.Sqrt(ratio) : ratio;
                targetHeightSize = FontSize * ratio;
            }

            FontSize = Math.Min(targetWidthSize, targetHeightSize);
        }
    }
}