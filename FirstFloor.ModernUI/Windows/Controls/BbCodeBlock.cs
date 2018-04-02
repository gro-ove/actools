using System;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Navigation;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls.BbCode;
using FirstFloor.ModernUI.Windows.Navigation;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls {
    /// <summary>
    /// A lighweight control for displaying small amounts of rich formatted BbCode content.
    /// </summary>
    [Localizable(false), ContentProperty(nameof(Text))]
    public class BbCodeBlock : PlaceholderTextBlock {
        [CanBeNull]
        public static IEmojiProvider OptionEmojiProvider;

        [CanBeNull]
        public static string OptionEmojiCacheDirectory;

        [CanBeNull]
        public static string OptionImageCacheDirectory;

        public static string Encode(string value) {
            return value?.Replace("[", "\\[");
        }

        public static string Decode(string value) {
            return value?.Replace("\\[", "[");
        }

        public static string EncodeAttribute(string value) {
            return value == null ? null : "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        public static event EventHandler<BbCodeImageEventArgs> ImageClicked;

        internal static void OnImageClicked(BbCodeImageEventArgs args) {
            ImageClicked?.Invoke(null, args);
        }

        public new static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(BbCodeBlock),
                new PropertyMetadata(OnBbCodeChanged));

        public new string Text {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly ILinkNavigator DefaultLinkNavigator = new DefaultLinkNavigator();

        public static void AddLinkCommand(Uri key, ICommand value) {
            DefaultLinkNavigator.Commands.Add(key, value);
        }

        public static readonly DependencyProperty LinkNavigatorProperty = DependencyProperty.Register(nameof(LinkNavigator), typeof(ILinkNavigator),
                typeof(BbCodeBlock), new PropertyMetadata(DefaultLinkNavigator, OnLinkNavigatorChanged));

        [CanBeNull]
        public ILinkNavigator LinkNavigator {
            get => (ILinkNavigator)GetValue(LinkNavigatorProperty);
            set => SetValue(LinkNavigatorProperty, value);
        }

        public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(nameof(Mode), typeof(EmojiSupport),
                typeof(BbCodeBlock), new PropertyMetadata(EmojiSupport.WithEmoji));

        public EmojiSupport Mode {
            get => (EmojiSupport)GetValue(ModeProperty);
            set => SetValue(ModeProperty, value);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BbCodeBlock"/> class.
        /// </summary>
        public BbCodeBlock() {
            // ensures the implicit BbCodeBlock style is used
            DefaultStyleKey = typeof(BbCodeBlock);

            AddHandler(FrameworkContentElement.LoadedEvent, new RoutedEventHandler(OnLoaded));
            AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(OnRequestNavigate));
        }

        private static void OnBbCodeChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((BbCodeBlock)o).UpdateDirty();
        }

        private static void OnLinkNavigatorChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((BbCodeBlock)o).UpdateDirty();
        }

        private bool _dirty;

        private void OnLoaded(object o, EventArgs e) {
            Update();
        }

        private void UpdateDirty() {
            _dirty = true;
            Update();
        }

        private static string EmojiToNumber(string emoji) {
            var result = new StringBuilder();

            for (var i = 0; i < emoji.Length; i++) {
                int code;
                if (char.IsHighSurrogate(emoji, i)) {
                    code = char.ConvertToUtf32(emoji, i);
                    i++;
                } else {
                    code = emoji[i];
                }

                if (code == 0x200d || code == 0xfe0f) {
                    continue;
                }

                if (result.Length > 0) {
                    result.Append('-');
                }

                result.Append(code.ToString("x"));
            }

            return result.ToString();
        }

        private static bool IsUrl(string s, int index, out int urlLength) {
            int start = index, length = s.Length;

            if (start > 0) {
                var previous = s[start - 1];
                if (previous == '=' || previous == '"' || char.IsLetterOrDigit(previous)) {
                    urlLength = 0;
                    return false;
                }
            }

            if (Expect("http")) {
                if (char.ToLowerInvariant(s[index]) == 's') {
                    index++;
                }
                if (!Expect("://")) {
                    urlLength = 0;
                    return false;
                }
            } else if (!Expect("www")) {
                urlLength = 0;
                return false;
            }

            var prefix = index - start;
            var last = '\0';
            for (char c; index < length && !char.IsWhiteSpace(c = s[index]); index++) {
                last = c;
            }

            urlLength = last == '.' || last == ',' || last == ':' || last == ';' || last == '!' || last == ']' ? index - start - 1 : index - start;
            return urlLength > prefix;

            bool Expect(string p) {
                for (var i = 0; i < p.Length; i++) {
                    var j = index + i;
                    if (j >= length || char.ToLowerInvariant(s[j]) != p[i]) return false;
                }

                index += p.Length;
                return true;
            }
        }

        [CanBeNull]
        private static Inline ParseEmojiOrNull(string bbCode, bool allowBbCodes, FrameworkElement element = null, ILinkNavigator navigator = null) {
            var converted = new StringBuilder();
            var lastIndex = 0;
            var complex = false;

            for (var i = 0; i < bbCode.Length; i++) {
                var c = bbCode[i];

                if (c == '[') {
                    if (!allowBbCodes) {
                        if (lastIndex < i) {
                            converted.Append(bbCode.Substring(lastIndex, i - lastIndex));
                        }
                        lastIndex = i + 1;
                        converted.Append(@"\[");
                    }
                    complex = true;
                    continue;
                }

                if (IsUrl(bbCode, i, out var urlLength)) {
                    var url = bbCode.Substring(i, urlLength);
                    converted.Append($"[url={EncodeAttribute(url)}]{Encode(url)}[/url]");
                    lastIndex = i + urlLength;
                    complex = true;
                }

                if (Emoji.IsEmoji(bbCode, i, out var emojiLength)) {
                    if (lastIndex < i) {
                        converted.Append(bbCode.Substring(lastIndex, i - lastIndex));
                    }
                    var emoji = bbCode.Substring(i, emojiLength);
                    converted.Append($"[img=\"emoji://{EmojiToNumber(emoji)}\"]{emoji}[/img]");
                    lastIndex = i + emojiLength;
                    complex = true;
                }

                // Even if it’s not an emoji, it still would be better to jump over high surrogates
                if (emojiLength > 1) {
                    i += emojiLength - 1;
                }
            }

            if (complex) {
                if (converted.Length > 0) {
                    converted.Append(bbCode.Substring(lastIndex));
                    bbCode = converted.ToString();
                }

                try {
                    return new BbCodeParser(bbCode, element) {
                        Commands = (navigator ?? DefaultLinkNavigator).Commands
                    }.Parse();
                } catch (Exception e) {
                    Logging.Error(e);
                }
            }

            return null;
        }

        [NotNull]
        public static Inline ParseEmoji(string bbCode, bool allowBbCodes, FrameworkElement element = null, ILinkNavigator navigator = null) {
            return ParseEmojiOrNull(bbCode, allowBbCodes, element, navigator) ?? new Run { Text = bbCode };
        }

        [CanBeNull]
        private static Inline ParseOrNull(string bbCode, FrameworkElement element = null, ILinkNavigator navigator = null) {
            if (bbCode.IndexOf('[') != -1) {
                try {
                    return new BbCodeParser(bbCode, element) {
                        Commands = (navigator ?? DefaultLinkNavigator).Commands
                    }.Parse();
                } catch (Exception e) {
                    Logging.Error(e);
                }
            }

            return null;
        }

        [NotNull]
        public static Inline Parse(string bbCode, FrameworkElement element = null, ILinkNavigator navigator = null) {
            return ParseOrNull(bbCode, element, navigator) ?? new Run { Text = bbCode };
        }

        private void Update() {
            if (!IsLoaded || !_dirty) return;

            var bbCode = Text;
            if (string.IsNullOrWhiteSpace(bbCode)) {
                SetPlaceholder();
            } else {
                var inlines = Inlines;
                var emojiSupport = Mode;

                var inline = emojiSupport == EmojiSupport.NoEmoji
                        ? ParseOrNull(bbCode, this, LinkNavigator)
                        : ParseEmojiOrNull(bbCode, emojiSupport != EmojiSupport.NothingButEmoji, this, LinkNavigator);
                if (inline == null) {
                    SetValue(TextBlock.TextProperty, bbCode);
                } else {
                    inlines.Clear();
                    inlines.Add(inline);
                }
            }

            _dirty = false;
        }

        private void OnRequestNavigate(object sender, RequestNavigateEventArgs e) {
            try {
                // perform navigation using the link navigator
                LinkNavigator?.Navigate(e.Uri, this, e.Target);
            } catch (Exception ex) {
                // display navigation failures
                Logging.Warning(ex);
                ModernDialog.ShowMessage(ex.Message, UiStrings.NavigationFailed, MessageBoxButton.OK);
            }
        }
    }
}