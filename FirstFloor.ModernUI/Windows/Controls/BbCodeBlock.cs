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

        public static readonly DependencyProperty BbCodeProperty = DependencyProperty.Register(nameof(BbCode), typeof(string), typeof(BbCodeBlock),
                new PropertyMetadata(OnBbCodeChanged));

        public string BbCode {
            get => (string)GetValue(BbCodeProperty);
            set => SetValue(BbCodeProperty, value);
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

        public static readonly DependencyProperty EmojiSupportProperty = DependencyProperty.Register(nameof(EmojiSupport), typeof(bool),
                typeof(BbCodeBlock), new PropertyMetadata(true));

        public bool EmojiSupport {
            get => GetValue(EmojiSupportProperty) as bool? == true;
            set => SetValue(EmojiSupportProperty, value);
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

                if (Emoji.IsEmoji(bbCode, i, out var length)) {
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
                LinkNavigator?.Navigate(e.Uri, this, e.Target);
            } catch (Exception ex) {
                // display navigation failures
                Logging.Warning(ex);
                ModernDialog.ShowMessage(ex.Message, UiStrings.NavigationFailed, MessageBoxButton.OK);
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
            IsDocumentEnabled = true;

            AddHandler(FrameworkContentElement.LoadedEvent, new RoutedEventHandler(OnLoaded));
            AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(OnRequestNavigate));

            /*CommandManager.RegisterClassCommandBinding(typeof(SelectableBbCodeBlock),
                    new CommandBinding(ApplicationCommands.Copy, OnCopy, OnCanExecuteCopy));*/
        }

        /*private static void OnCanExecuteCopy(object target, CanExecuteRoutedEventArgs args) {
            var block = (SelectableBbCodeBlock)target;
            args.CanExecute = block.IsEnabled && !block.Selection.IsEmpty;
        }

        private static void OnCopy(object sender, ExecutedRoutedEventArgs e) {
            var block = (SelectableBbCodeBlock)sender;
            Clipboard.SetText(GetInlineText(block));
            e.Handled = true;
        }

        private static string GetInlineText(RichTextBox block) {
            var b = new StringBuilder();
            var s = block.Selection;

            void Process(Inline inline) {
                if (!s.Contains(inline.ContentStart)) return;

                s.Text

                if (inline is InlineUIContainer uiContainer){
                    b.Append(uiContainer.Tag);
                } else  if (inline is Span span) {
                    foreach (var i in span.Inlines) {
                        Process(i);
                    }
                } else if (inline is Run) {
                    var run = (Run)inline;
                    b.Append(run.Text);
                } else if (inline is LineBreak) {
                    b.Append(Environment.NewLine);
                } else {
                    Logging.Debug(inline.GetType().Name);
                }
            }

            foreach (var p in block.Document.Blocks.OfType<Paragraph>()) {
                foreach (var inline in p.Inlines) {
                    Process(inline);
                }
            }
            return b.ToString();
        }*/

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

        public static readonly DependencyProperty LineHeightProperty = DependencyProperty.Register(nameof(LineHeight), typeof(double),
                typeof(SelectableBbCodeBlock), new PropertyMetadata(double.NaN, (o, e) => {
                    ((SelectableBbCodeBlock)o)._lineHeight = (double)e.NewValue;
                }));

        private double _lineHeight = double.NaN;

        public double LineHeight {
            get => _lineHeight;
            set => SetValue(LineHeightProperty, value);
        }

        private void Update() {
            if (!IsLoaded || !_dirty) {
                return;
            }

            var bbCode = BbCode;

            Document.Blocks.Clear();
            if (!string.IsNullOrWhiteSpace(bbCode)) {
                var item = new Paragraph(BbCodeBlock.ParseEmoji(bbCode, this, LinkNavigator)) {
                    TextAlignment = TextAlignment.Left
                };

                if (!double.IsNaN(LineHeight)) {
                    item.LineHeight = LineHeight;
                }

                Document.Blocks.Add(item);
            }

            _dirty = false;
        }

        private void OnRequestNavigate(object sender, RequestNavigateEventArgs e) {
            try {
                // perform navigation using the link navigator
                LinkNavigator.Navigate(e.Uri, this, e.Target);
            } catch (Exception ex) {
                // display navigation failures
                Logging.Warning(ex);
                ModernDialog.ShowMessage(ex.Message, UiStrings.NavigationFailed, MessageBoxButton.OK);
            }
        }

        /// <summary>
        /// Gets or sets the BB code.
        /// </summary>
        /// <value>The BB code.</value>
        public string BbCode {
            get => (string)GetValue(BbCodeProperty);
            set => SetValue(BbCodeProperty, value);
        }

        /// <summary>
        /// Gets or sets the link navigator.
        /// </summary>
        /// <value>The link navigator.</value>
        public ILinkNavigator LinkNavigator {
            get => (ILinkNavigator)GetValue(LinkNavigatorProperty);
            set => SetValue(LinkNavigatorProperty, value);
        }
    }
}
