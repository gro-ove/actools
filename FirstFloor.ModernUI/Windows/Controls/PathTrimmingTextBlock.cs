using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class PathTrimmingTextBlock : RichTextBox, IValueConverter {
        public PathTrimmingTextBlock() {
            DefaultStyleKey = typeof(PathTrimmingTextBlock);

            Loaded += OnLoaded;
            SizeChanged += OnSizeChanged;
            Background = new SolidColorBrush(Colors.Transparent);
            // TextTrimming = TextTrimming.CharacterEllipsis;

            DataObject.AddCopyingHandler(this, CopyCommand);
            MouseDown += OnMouseDown;

            HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
            Document.PageWidth = 2000;
        }

        private void OnMouseDown(object sender, MouseButtonEventArgs mouseButtonEventArgs) {
            SelectAll();
        }

        private void CopyCommand(object sender, DataObjectEventArgs e) {
            e.Handled = true;
            e.CancelCommand();
            Clipboard.SetText(Text);
        }

        private void Update() {
            var text = Text;
            if (string.IsNullOrEmpty(text)) {
                ToolTip = null;
                SetPlaceholder();
            } else {
                var trimmed = GetTrimmedPath(ActualWidth);
                ToolTip = trimmed == text ? null : text;

                Document.Blocks.Clear();
                Document.Blocks.Add(new Paragraph(new Run(trimmed)) {
                    TextAlignment = TextAlignment.Left
                });
            }
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs sizeChangedEventArgs) {
            if (IsLoaded) {
                Update();
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            Update();
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

        protected void SetPlaceholder() {
            Document.Blocks.Clear();
            var placeholder = Placeholder;
            if (!string.IsNullOrEmpty(placeholder)) {
                var inline = new Run { Text = placeholder };
                inline.SetBinding(TextElement.ForegroundProperty, new Binding {
                    Path = new PropertyPath(nameof(Foreground)),
                    Source = this,
                    Converter = this
                });
                Document.Blocks.Add(new Paragraph(inline) {
                    TextAlignment = TextAlignment.Left
                });
            }
        }

        public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string),
                typeof(PathTrimmingTextBlock), new PropertyMetadata(OnPlaceholderChanged));

        public string Placeholder {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        private static void OnPlaceholderChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((PathTrimmingTextBlock)o).Update();
        }

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string),
                typeof(PathTrimmingTextBlock), new PropertyMetadata(OnTextChanged));

        private string _text;
        public string Text {
            get => _text;
            set => SetValue(TextProperty, value);
        }

        private static void OnTextChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((PathTrimmingTextBlock)o).OnTextChanged((string)e.NewValue);
        }

        private void OnTextChanged(string newValue) {
            _text = newValue;

            if (IsLoaded) {
                Update();
            }
        }

        private static List<string> Split(string s, out bool url) {
            url = false;

            var p = 0;
            var r = new List<string>(10);
            for (var i = 0; i < s.Length; i++) {
                var c = s[i];
                if (c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar) {
                    var l = i - p;
                    if (l > 0) {
                        var piece = s.Substring(p, i - p);
                        if (p == 0 && (piece == "http:" || piece == "https:") && i < s.Length - 1 && s[i + 1] == '/') {
                            r.Add(piece + '/');
                            url = true;
                            ++i;
                        } else {
                            r.Add(piece);
                        }
                    }

                    p = i + 1;
                }
            }

            if (p < s.Length - 1) {
                r.Add(s.Substring(p));
            }

            return r;
        }

        private string GetTrimmedPath(double width) {
            if (Text == null) return "";

            width -= 20d;

            var fontStyle = FontStyle;
            var fontWeight = FontWeight;
            var fontStretch = FontStretch;
            var fontFamily = FontFamily.GetTypefaces().FirstOrDefault(x => x.Style == fontStyle && x.Weight == fontWeight && x.Stretch == fontStretch) ??
                    FontFamily.GetTypefaces().First();
            var fontSize = FontSize;
            var foreground = Foreground;

            bool TestWidth(string text) {
                return new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                        fontFamily, fontSize, foreground).Width < width;
            }

            if (TestWidth(Text)) return Text;

            var pieces = Split(Text, out bool url);
            if (pieces.Count == 0) return "";

            var c = url ? '/' : Path.DirectorySeparatorChar;
            if (pieces.Count == 1) return Text;

            if (url) {
                var last = pieces[pieces.Count - 1];
                for (var i = 1; i < pieces.Count - 1; i++) {
                    var candidate = string.Join(c.ToString(),
                            pieces.Take(pieces.Count - i - 1).Concat(new[]{ "…", last }));
                    if (TestWidth(candidate)) return candidate;
                }
                return $@"…{c}{last}";
            }

            for (var i = 1; i < pieces.Count - 1; i++) {
                var candidate = string.Join(c.ToString(),
                        new[] { pieces[0], "…" }.Concat(pieces.Skip(i + 1)));
                if (TestWidth(candidate)) return candidate;
            }

            return $@"…{c}{pieces[pieces.Count - 1]}";
        }
    }
}