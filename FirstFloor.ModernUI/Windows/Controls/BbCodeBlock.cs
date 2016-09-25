using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Navigation;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Controls.BbCode;
using FirstFloor.ModernUI.Windows.Navigation;

namespace FirstFloor.ModernUI.Windows.Controls {
    /// <summary>
    /// A lighweight control for displaying small amounts of rich formatted BbCode content.
    /// </summary>
    [Localizable(false), ContentProperty(nameof(BbCode))]
    public class BbCodeBlock : PlaceholderTextBlock {
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

        internal static readonly ILinkNavigator DefaultLinkNavigator = new DefaultLinkNavigator();

        /// <summary>
        /// Identifies the BbCode dependency property.
        /// </summary>
        public static readonly DependencyProperty BbCodeProperty = DependencyProperty.Register(nameof(BbCode), typeof(string), typeof(BbCodeBlock),
                new PropertyMetadata(OnBbCodeChanged));

        /// <summary>
        /// Identifies the LinkNavigator dependency property.
        /// </summary>
        public static readonly DependencyProperty LinkNavigatorProperty = DependencyProperty.Register(nameof(LinkNavigator), typeof(ILinkNavigator),
                typeof(BbCodeBlock), new PropertyMetadata(DefaultLinkNavigator, OnLinkNavigatorChanged));

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

        public static Inline Parse(string bbCode, FrameworkElement element = null, ILinkNavigator navigator = null) {
            try {
                var parser = new BbCodeParser(bbCode, element) {
                    Commands = (navigator ?? DefaultLinkNavigator).Commands
                };
                return parser.Parse();
            } catch (Exception e) {
                Logging.Warning(e);
                return new Run { Text = bbCode };
            }
        }

        private void Update() {
            if (!IsLoaded || !_dirty) {
                return;
            }

            var bbCode = BbCode;
            if (string.IsNullOrWhiteSpace(bbCode)) {
                SetPlaceholder();
            } else {
                Inlines.Clear();
                Inlines.Add(Parse(bbCode, this, LinkNavigator));
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
