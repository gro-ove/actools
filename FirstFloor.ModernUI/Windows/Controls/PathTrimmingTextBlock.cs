using System;
using System.Collections.Generic;
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

        private List<string> Split(string s, out bool url) {
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