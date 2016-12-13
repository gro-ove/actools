using System;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
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
    [Localizable(false), ContentProperty(nameof(BbCode))]
    public class BbCodeBlock : PlaceholderTextBlock {
        [CanBeNull]
        public static string OptionEmojiProvider;

        [CanBeNull]
        public static string OptionEmojiCacheDirectory;

        [CanBeNull]
        public static string OptionImageCacheDirectory;

        public static string Encode(string value) {
            return value?.Replace("[", "\\[");
        }

        public static string EncodeAttribute(string value) {
            return value == null ? null : "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        public static event EventHandler<BbCodeImageEventArgs> ImageClicked;

        internal static void OnImageClicked(BbCodeImageEventArgs args) {
            ImageClicked?.Invoke(null, args);
        }
        
        public static readonly DependencyProperty BbCodeProperty = DependencyProperty.Register(nameof(BbCode), typeof(string), typeof(BbCodeBlock),
                new PropertyMetadata(OnBbCodeChanged));

        public string BbCode {
            get { return (string)GetValue(BbCodeProperty); }
            set { SetValue(BbCodeProperty, value); }
        }

        internal static readonly ILinkNavigator DefaultLinkNavigator = new DefaultLinkNavigator();

        public static readonly DependencyProperty LinkNavigatorProperty = DependencyProperty.Register(nameof(LinkNavigator), typeof(ILinkNavigator),
                typeof(BbCodeBlock), new PropertyMetadata(DefaultLinkNavigator, OnLinkNavigatorChanged));
        
        public ILinkNavigator LinkNavigator {
            get { return (ILinkNavigator)GetValue(LinkNavigatorProperty); }
            set { SetValue(LinkNavigatorProperty, value); }
        }

        public static readonly DependencyProperty EmojiSupportProperty = DependencyProperty.Register(nameof(EmojiSupport), typeof(bool),
                typeof(BbCodeBlock), new PropertyMetadata(true));

        public bool EmojiSupport {
            get { return (bool)GetValue(EmojiSupportProperty); }
            set { SetValue(EmojiSupportProperty, value); }
        }

        private bool _dirty;

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
            if (e.NewValue == null) {
                // null values disallowed
                throw new NullReferenceException("LinkNavigator");
            }

            ((BbCodeBlock)o).UpdateDirty();
        }

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
                if (Char.IsHighSurrogate(emoji, i)) {
                    code = Char.ConvertToUtf32(emoji, i);
                    i++;
                } else {
                    code = emoji[i];
                }

                if (code == 0x200d) continue;

                if (result.Length > 0) {
                    result.Append('-');
                }

                result.Append(code.ToString("x"));
            }

            return result.ToString();
        }

        public static Inline ParseEmoji(string bbCode, FrameworkElement element = null, ILinkNavigator navigator = null) {
            var converted = new StringBuilder();
            var lastIndex = 0;
            var complex = false;

            for (var i = 0; i < bbCode.Length; i++) {
                var c = bbCode[i];

                if (c == '[') {
                    complex = true;
                    continue;
                }

                int length;
                if (Emoji.IsEmoji(bbCode, i, out length)) {
                    if (lastIndex != i) {
                        converted.Append(bbCode.Substring(lastIndex, i - lastIndex));
                    }

                    var emoji = bbCode.Substring(i, length);
                    converted.Append($"[img=\"emoji://{EmojiToNumber(emoji)}\"]{emoji}[/img]");
                    lastIndex = i + length;
                    complex = true;
                }

                // Even if it’s not an emoji, it still would be better to jump over high surrogates
                if (length > 1) {
                    i += length - 1;
                }
            }

            if (converted.Length > 0) {
                converted.Append(bbCode.Substring(lastIndex));
                bbCode = converted.ToString();
            }
            
            if (complex) {
                try {
                    return new BbCodeParser(bbCode, element) {
                        Commands = (navigator ?? DefaultLinkNavigator).Commands
                    }.Parse();
                } catch (Exception e) {
                    Logging.Error(e);
                }
            }
            
            return new Run { Text = bbCode };
        }

        public static Inline Parse(string bbCode, FrameworkElement element = null, ILinkNavigator navigator = null) {
            if (bbCode.IndexOf('[') != -1) {
                try {
                    return new BbCodeParser(bbCode, element) {
                        Commands = (navigator ?? DefaultLinkNavigator).Commands
                    }.Parse();
                } catch (Exception e) {
                    Logging.Error(e);
                }
            }

            return new Run { Text = bbCode };
        }

        private void Update() {
            if (!IsLoaded || !_dirty) return;

            var bbCode = BbCode;
            if (string.IsNullOrWhiteSpace(bbCode)) {
                SetPlaceholder();
            } else {
                Inlines.Clear();
                Inlines.Add(EmojiSupport ? ParseEmoji(bbCode, this, LinkNavigator) : Parse(bbCode, this, LinkNavigator));
            }

            _dirty = false;
        }

        private void OnRequestNavigate(object sender, RequestNavigateEventArgs e) {
            try {
                // perform navigation using the link navigator
                LinkNavigator.Navigate(e.Uri, this, e.Target);
            } catch (Exception error) {
                // display navigation failures
                ModernDialog.ShowMessage(error.Message, UiStrings.NavigationFailed, MessageBoxButton.OK);
            }
        }
    }

    /// <summary>
    /// Alternative version with selection support (totally different underneath).
    /// </summary>
    [Localizable(false), ContentProperty(nameof(BbCode))]
    public class SelectableBbCodeBlock : RichTextBox {
        /// <summary>
        /// Identifies the BbCode dependency property.
        /// </summary>
        public static readonly DependencyProperty BbCodeProperty = DependencyProperty.Register(nameof(BbCode), typeof(string), typeof(SelectableBbCodeBlock),
                new PropertyMetadata(OnBbCodeChanged));

        /// <summary>
        /// Identifies the LinkNavigator dependency property.
        /// </summary>
        public static readonly DependencyProperty LinkNavigatorProperty = DependencyProperty.Register(nameof(LinkNavigator), typeof(ILinkNavigator),
                typeof(SelectableBbCodeBlock), new PropertyMetadata(BbCodeBlock.DefaultLinkNavigator, OnLinkNavigatorChanged));

        private bool _dirty;

        /// <summary>
        /// Initializes a new instance of the <see cref="BbCodeBlock"/> class.
        /// </summary>
        public SelectableBbCodeBlock() {
            // ensures the implicit BbCodeBlock style is used
            DefaultStyleKey = typeof(SelectableBbCodeBlock);

            AddHandler(FrameworkContentElement.LoadedEvent, new RoutedEventHandler(OnLoaded));
            AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(OnRequestNavigate));
        }

        private static void OnBbCodeChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((SelectableBbCodeBlock)o).UpdateDirty();
        }

        private static void OnLinkNavigatorChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            if (e.NewValue == null) {
                // null values disallowed
                throw new NullReferenceException("LinkNavigator");
            }

            ((SelectableBbCodeBlock)o).UpdateDirty();
        }

        private void OnLoaded(object o, EventArgs e) {
            Update();
        }

        private void UpdateDirty() {
            _dirty = true;
            Update();
        }

        private void Update() {
            if (!IsLoaded || !_dirty) {
                return;
            }

            var bbCode = BbCode;

            Document.Blocks.Clear();
            if (!string.IsNullOrWhiteSpace(bbCode)) {
                Document.Blocks.Add(new Paragraph(BbCodeBlock.Parse(bbCode, this, LinkNavigator)));
            }

            _dirty = false;
        }

        private void OnRequestNavigate(object sender, RequestNavigateEventArgs e) {
            try {
                // perform navigation using the link navigator
                LinkNavigator.Navigate(e.Uri, this, e.Target);
            } catch (Exception error) {
                // display navigation failures
                ModernDialog.ShowMessage(error.Message, UiStrings.NavigationFailed, MessageBoxButton.OK);
            }
        }

        /// <summary>
        /// Gets or sets the BB code.
        /// </summary>
        /// <value>The BB code.</value>
        public string BbCode {
            get { return (string)GetValue(BbCodeProperty); }
            set { SetValue(BbCodeProperty, value); }
        }

        /// <summary>
        /// Gets or sets the link navigator.
        /// </summary>
        /// <value>The link navigator.</value>
        public ILinkNavigator LinkNavigator {
            get { return (ILinkNavigator)GetValue(LinkNavigatorProperty); }
            set { SetValue(LinkNavigatorProperty, value); }
        }
    }
}
