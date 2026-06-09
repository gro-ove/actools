using System;
using System.ComponentModel;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Navigation;
using FirstFloor.ModernUI.Commands;
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
        public static ResourceDictionary IconsDictionary { get; set; }
        
        public enum AllowBbCodes {
            None,
            Limited,
            All
        }

        public static ICommand LinkCommand { get; } = new AsyncCommand<string>(async p => {
            var uri = new Uri(p, UriKind.RelativeOrAbsolute);
            if (DefaultLinkNavigator.Commands != null) {
                if (DefaultLinkNavigator.Commands.TryGetValue(uri, out var command)) {
                    await ExecuteCommandAsync(command, null);
                    return;
                }

                if (uri.IsAbsoluteUri) {
                    var original = uri.AbsoluteUri;
                    var index = original.IndexOf('?');
                    if (index != -1) {
                        var subUri = new Uri(original.Substring(0, index), UriKind.Absolute);
                        if (DefaultLinkNavigator.Commands.TryGetValue(subUri, out command)) {
                            await ExecuteCommandAsync(command, uri.GetQueryParam("param"));
                            return;
                        }
                    }
                }
            }

            DefaultLinkNavigator?.Navigate(uri, Application.Current?.MainWindow);

            Task ExecuteCommandAsync(ICommand command, string param) {
                if (!command.CanExecute(param)) return Task.Delay(0);

                if (command is AsyncCommand asyncCommand) {
                    return asyncCommand.ExecuteAsync();
                }

                if (command is AsyncCommand<string> asyncCommandT) {
                    return asyncCommandT.ExecuteAsync(param);
                }

                command.Execute(param);
                return Task.Delay(0);
            }
        });

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
            DefaultLinkNavigator.Commands.Remove(key);
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
                typeof(BbCodeBlock), new PropertyMetadata(EmojiSupport.Extended, OnBbCodeChanged));

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

        public static readonly DependencyProperty HighlightUrlsProperty = DependencyProperty.Register(nameof(HighlightUrls), typeof(bool),
                typeof(BbCodeBlock), new PropertyMetadata(true, (o, e) => {
                    var block = (BbCodeBlock)o;
                    block._highlightUrls = (bool)e.NewValue;
                    block.UpdateDirty();
                }));

        private bool _highlightUrls = true;

        public bool HighlightUrls {
            get => _highlightUrls;
            set => SetValue(HighlightUrlsProperty, value);
        }

        public static readonly DependencyProperty AllowImagesProperty = DependencyProperty.Register(nameof(AllowImages), typeof(bool),
                typeof(BbCodeBlock), new PropertyMetadata(true, (o, e) => {
                    ((BbCodeBlock)o)._allowImages = (bool)e.NewValue;
                }));

        private bool _allowImages = true;

        public bool AllowImages {
            get => _allowImages;
            set => SetValue(AllowImagesProperty, value);
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

        private static bool IsMatching(string bbCode, int index, string compareWith) {
            if (index + compareWith.Length > bbCode.Length) return false;
            for (var i = 0; i < compareWith.Length; i++) {
                if (bbCode[index + i] != compareWith[i]) return false;
            }
            return true;
        }

        private static bool IsLimitedBbCode(string bbCode, int index) {
            if (index + 2 > bbCode.Length) return false;
            if (bbCode[index] == '/') {
                if (++index + 2 > bbCode.Length) return false;
            }
            switch (bbCode[index]) {
                case 's':
                    return IsMatching(bbCode, index, "size");
                case 'u':
                    return IsMatching(bbCode, index, "url");
                case 'i':
                    return IsMatching(bbCode, index, "img") || IsMatching(bbCode, index, "ico");
                default:
                    return false;
            }
        }

        private static bool ContainsEmoji(string bbCode) {
            for (var i = 0; i < bbCode.Length; i++) {
                if (Emoji.IsEmoji(bbCode, i, out var emojiLength)) {
                    return true;
                }
                if (emojiLength > 1) {
                    i += emojiLength - 1;
                }
            }
            return false;
        }

        [CanBeNull]
        private static Inline ParseWithParser(string bbCode, FrameworkElement element, ILinkNavigator navigator, bool allowImages) {
            try {
                return new BbCodeParser(bbCode, element) {
                    Commands = (navigator ?? DefaultLinkNavigator).Commands,
                    AllowImages = allowImages
                }.Parse();
            } catch (Exception e) {
                Logging.Error(e);
                return null;
            }
        }

        private static void AppendSegment(StringBuilder sb, string bbCode, int from, int to) {
            if (from < to) {
                sb.Append(bbCode, from, to - from);
            }
        }

        [CanBeNull]
        private static Inline ParseEmojiOrNull(string bbCode, AllowBbCodes allowBbCodes, bool highlightUrls, bool allowImages, FrameworkElement element = null,
                ILinkNavigator navigator = null) {
            try {
                var needsEscape = allowBbCodes != AllowBbCodes.All;
                var bracketIndex = bbCode.IndexOf('[');

                if (!needsEscape && !highlightUrls) {
                    if (bracketIndex == -1) {
                        if (!ContainsEmoji(bbCode)) {
                            return null;
                        }
                    } else if (!ContainsEmoji(bbCode)) {
                        return ParseWithParser(bbCode, element, navigator, allowImages);
                    }
                }

                StringBuilder converted = null;
                var lastIndex = 0;
                var complex = bracketIndex != -1;
                var urlSkipNext = false;

                for (var i = 0; i < bbCode.Length; i++) {
                    var c = bbCode[i];

                    if (c == '[') {
                        if (needsEscape && (allowBbCodes == AllowBbCodes.None || !IsLimitedBbCode(bbCode, i + 1))) {
                            if (converted == null) converted = new StringBuilder();
                            AppendSegment(converted, bbCode, lastIndex, i);
                            lastIndex = i + 1;
                            converted.Append(@"\[");
                        }
                        complex = true;
                        urlSkipNext = false;
                        continue;
                    }

                    if (highlightUrls) {
                        if (char.IsLetterOrDigit(c)) {
                            if (!urlSkipNext && UrlHelper.IsWebUrl(bbCode, i, true, out var urlLength)) {
                                if (converted == null) converted = new StringBuilder();
                                AppendSegment(converted, bbCode, lastIndex, i);
                                var url = bbCode.Substring(i, urlLength);
                                lastIndex = i + urlLength;
                                converted.Append("[url=").Append(EncodeAttribute(url.Urlify())).Append(']').Append(Encode(url)).Append("[/url]");
                                complex = true;
                            }

                            urlSkipNext = true;
                        } else {
                            urlSkipNext = false;
                        }
                    }

                    if (Emoji.IsEmoji(bbCode, i, out var emojiLength)) {
                        if (converted == null) converted = new StringBuilder();
                        AppendSegment(converted, bbCode, lastIndex, i);
                        var emoji = bbCode.Substring(i, emojiLength);
                        lastIndex = i + emojiLength;
                        converted.Append("[img=\"emoji://").Append(EmojiToNumber(emoji)).Append("\"]").Append(emoji).Append("[/img]");
                        complex = true;
                    }

                    // Even if it’s not an emoji, it still would be better to jump over high surrogates
                    if (emojiLength > 1) {
                        i += emojiLength - 1;
                    }
                }

                if (!complex) {
                    return null;
                }

                if (converted != null && converted.Length > 0) {
                    AppendSegment(converted, bbCode, lastIndex, bbCode.Length);
                    bbCode = converted.ToString();
                }

                return ParseWithParser(bbCode, element, navigator, allowImages);
            } catch (Exception e) {
                Logging.Warning(e);
                Logging.Warning(bbCode);
                return null;
            }
        }

        [NotNull]
        public static Inline ParseEmoji(string bbCode, AllowBbCodes allowBbCodes, bool highlightUrls, FrameworkElement element = null,
                ILinkNavigator navigator = null) {
            return ParseEmojiOrNull(bbCode, allowBbCodes, highlightUrls, true, element, navigator) ?? new Run { Text = bbCode };
        }

        [CanBeNull]
        private static Inline ParseOrNull(string bbCode, FrameworkElement element = null, ILinkNavigator navigator = null) {
            return bbCode.IndexOf('[') == -1 ? null : ParseWithParser(bbCode, element, navigator, true);
        }

        [NotNull]
        public static Inline Parse(string bbCode, FrameworkElement element = null, ILinkNavigator navigator = null) {
            return ParseOrNull(bbCode, element, navigator) ?? new Run { Text = bbCode };
        }

        public void ForceUpdate() {
            var bbCode = Text;
            if (string.IsNullOrWhiteSpace(bbCode)) {
                SetPlaceholder();
            } else {
                var inlines = Inlines;
                var emojiSupport = Mode;
                var highlightUrls = HighlightUrls;

                var inline = emojiSupport == EmojiSupport.Simple
                        ? ParseOrNull(bbCode, this, LinkNavigator)
                        : ParseEmojiOrNull(bbCode,
                                emojiSupport == EmojiSupport.SafeBbCodes ? AllowBbCodes.Limited :
                                        emojiSupport == EmojiSupport.WithoutBbCodes ? AllowBbCodes.None : AllowBbCodes.All,
                                highlightUrls, _allowImages, this, LinkNavigator);
                if (inline == null) {
                    SetValue(TextBlock.TextProperty, bbCode);
                } else {
                    inlines.Clear();
                    inlines.Add(inline);
                }
            }

            _dirty = false;
        }

        private void Update() {
            if (!IsLoaded || !_dirty) return;
            ForceUpdate();
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