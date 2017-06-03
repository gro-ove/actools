using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace FirstFloor.ModernUI.Windows.Controls {
    public class PathTrimmingTextBlock : TextBlock {
        public PathTrimmingTextBlock() {
            Loaded += OnLoaded;
            SizeChanged += OnSizeChanged;
            Background = new SolidColorBrush(Colors.Transparent);
            TextTrimming = TextTrimming.CharacterEllipsis;
        }

        private void Update() {
            var trimmed = GetTrimmedPath(ActualWidth);
            ToolTip = trimmed == Text ? null : Text;
            base.Text = trimmed;
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs sizeChangedEventArgs) {
            if (IsLoaded) {
                Update();
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            Update();
        }

        public new static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string),
                typeof(PathTrimmingTextBlock), new PropertyMetadata(OnTextChanged));

        private string _text;
        public new string Text {
            get { return _text; }
            set { SetValue(TextProperty, value); }
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

            var pieces = Text.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                    StringSplitOptions.RemoveEmptyEntries);
            if (pieces.Length == 1) return Text;

            for (var i = 1; i < pieces.Length - 1; i++) {
                var candidate = string.Join(Path.DirectorySeparatorChar.ToString(),
                        new[] { pieces[0], "…" }.Concat(pieces.Skip(i + 1)));
                if (TestWidth(candidate)) return candidate;
            }

            return $@"…{Path.DirectorySeparatorChar}{pieces[pieces.Length - 1]}";
        }
    }
}