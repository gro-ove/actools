using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Navigation;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Navigation;

namespace FirstFloor.ModernUI.Windows.Controls {
    /// <summary>
    /// Alternative version with selection support (totally different underneath).
    /// </summary>
    [Localizable(false), ContentProperty(nameof(Text))]
    public class SelectableBbCodeBlock : RichTextBox {
        /// <summary>
        /// Identifies the BbCode dependency property.
        /// </summary>
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(SelectableBbCodeBlock),
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
            DataObject.AddCopyingHandler(this, OnCopy);
        }

        private void OnCopy(object o, DataObjectCopyingEventArgs e) {
            var clipboard = "";

            for (TextPointer p = Selection.Start, next; p != null && p.CompareTo(Selection.End) < 0; p = next) {
                next = p.GetNextInsertionPosition(LogicalDirection.Forward);
                if (next == null) break;

                var textRange = new TextRange(p, next);
                clipboard += textRange.Start.Parent is EmojiSpan span ? span.Text : textRange.Text;
            }

            ClipboardHelper.SetText(clipboard);
            e.Handled = true;
            e.CancelCommand();
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

        public static readonly DependencyProperty LineHeightProperty = DependencyProperty.Register(nameof(LineHeight), typeof(double),
                typeof(SelectableBbCodeBlock), new PropertyMetadata(double.NaN, (o, e) => { ((SelectableBbCodeBlock)o)._lineHeight = (double)e.NewValue; }));

        private double _lineHeight = double.NaN;

        public double LineHeight {
            get => _lineHeight;
            set => SetValue(LineHeightProperty, value);
        }

        public static readonly DependencyProperty ModeProperty = DependencyProperty.Register(nameof(Mode), typeof(EmojiSupport),
                typeof(SelectableBbCodeBlock), new PropertyMetadata(EmojiSupport.Extended));

        public EmojiSupport Mode {
            get => (EmojiSupport)GetValue(ModeProperty);
            set => SetValue(ModeProperty, value);
        }

        public static readonly DependencyProperty HighlightUrlsProperty = DependencyProperty.Register(nameof(HighlightUrls), typeof(bool),
                typeof(SelectableBbCodeBlock), new PropertyMetadata(true, (o, e) => ((SelectableBbCodeBlock)o)._highlightUrls = (bool)e.NewValue));

        private bool _highlightUrls = true;

        public bool HighlightUrls {
            get => _highlightUrls;
            set => SetValue(HighlightUrlsProperty, value);
        }

        private void Update() {
            if (!IsLoaded || !_dirty) {
                return;
            }

            var bbCode = Text;

            Document.Blocks.Clear();
            if (!string.IsNullOrWhiteSpace(bbCode)) {
                var emojiSupport = Mode;
                var item = new Paragraph(emojiSupport == EmojiSupport.Simple
                        ? BbCodeBlock.Parse(bbCode, this, LinkNavigator)
                        : BbCodeBlock.ParseEmoji(bbCode,
                                emojiSupport == EmojiSupport.SafeBbCodes ? BbCodeBlock.AllowBbCodes.Limited :
                                        emojiSupport == EmojiSupport.WithoutBbCodes ? BbCodeBlock.AllowBbCodes.None : BbCodeBlock.AllowBbCodes.All,
                                HighlightUrls, this, LinkNavigator)) {
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
        public string Text {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
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