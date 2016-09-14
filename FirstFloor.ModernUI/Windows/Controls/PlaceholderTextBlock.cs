using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class PlaceholderTextBlock : TextBlock, IValueConverter {
        public new static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string),
                typeof(PlaceholderTextBlock), new PropertyMetadata(OnTextChanged));

        public new string Text {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        private static void OnTextChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((PlaceholderTextBlock)o).Update();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            var brush = value as SolidColorBrush;
            if (brush == null) return new SolidColorBrush();

            var color = brush.Color;
            return new SolidColorBrush(Color.FromArgb((byte)(color.A / 2.7), color.R, color.G, color.B));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotSupportedException();
        }

        private void Update() {
            var text = Text;
            if (string.IsNullOrEmpty(text)) {
                Inlines.Clear();

                var placeholder = Placeholder;
                if (!string.IsNullOrEmpty(placeholder)) {
                    var inline = new Run { Text = placeholder };
                    inline.SetBinding(TextElement.ForegroundProperty, new Binding {
                        Path = new PropertyPath(nameof(Foreground)),
                        Source = this,
                        Converter = this
                    });
                    Inlines.Add(inline);
                }
            } else {
                SetValue(TextBlock.TextProperty, text);
            }
        }

        public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string),
                typeof(PlaceholderTextBlock), new PropertyMetadata(OnPlaceholderChanged));

        public string Placeholder {
            get { return (string)GetValue(PlaceholderProperty); }
            set { SetValue(PlaceholderProperty, value); }
        }

        private static void OnPlaceholderChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((PlaceholderTextBlock)o).Update();
        }
    }
}