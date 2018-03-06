using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class PlaceholderTextBlock : TextBlock {
        public new static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string),
                typeof(PlaceholderTextBlock), new PropertyMetadata(OnTextChanged));

        public new string Text {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        private static void OnTextChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((PlaceholderTextBlock)o).Update();
        }

        private static readonly ValueConverter Converter = new ValueConverter();

        private class ValueConverter : IValueConverter {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
                if (!(value is SolidColorBrush brush)) return new SolidColorBrush();
                var color = brush.Color;
                return new SolidColorBrush(Color.FromArgb((byte)(color.A / 2.7), color.R, color.G, color.B));
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
                throw new NotSupportedException();
            }
        }

        public static Inline GetPlaceholder(FrameworkElement target, string text) {
            var inline = new Run { Text = text };
            inline.SetBinding(TextElement.ForegroundProperty, new Binding {
                Path = new PropertyPath(nameof(Foreground)),
                Source = target,
                Converter = Converter
            });
            return inline;
        }

        protected void SetPlaceholder() {
            var inlines = Inlines;
            inlines.Clear();
            var placeholder = Placeholder;
            if (!string.IsNullOrEmpty(placeholder)) {
                inlines.Add(GetPlaceholder(this, placeholder));
            }
        }

        private void Update() {
            var text = Text;
            if (string.IsNullOrEmpty(text)) {
                SetPlaceholder();
            } else {
                SetValue(TextBlock.TextProperty, text);
            }
        }

        public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string),
                typeof(PlaceholderTextBlock), new PropertyMetadata(OnPlaceholderChanged));

        public string Placeholder {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        private static void OnPlaceholderChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((PlaceholderTextBlock)o).Update();
        }
    }
}