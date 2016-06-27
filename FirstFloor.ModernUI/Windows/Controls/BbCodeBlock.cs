using System;
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
    [ContentProperty("BbCode")]
    public class BbCodeBlock : TextBlock {
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

        /// <summary>
        /// Identifies the BbCode dependency property.
        /// </summary>
        public static DependencyProperty BbCodeProperty = DependencyProperty.Register("BbCode", typeof(string), typeof(BbCodeBlock), new PropertyMetadata(OnBbCodeChanged));
        /// <summary>
        /// Identifies the LinkNavigator dependency property.
        /// </summary>
        public static DependencyProperty LinkNavigatorProperty = DependencyProperty.Register("LinkNavigator", typeof(ILinkNavigator), typeof(BbCodeBlock), new PropertyMetadata(new DefaultLinkNavigator(), OnLinkNavigatorChanged));

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

        private void Update() {
            if (!IsLoaded || !_dirty) {
                return;
            }

            var bbcode = BbCode;

            Inlines.Clear();

            if (!string.IsNullOrWhiteSpace(bbcode)) {
                Inline inline;
                try {
                    var parser = new BbCodeParser(bbcode, this) {
                        Commands = LinkNavigator.Commands
                    };
                    inline = parser.Parse();
                } catch (Exception e) {
                    Logging.Warning("[BBCODEBLOCK] Parsing failed: " + e);
                    inline = new Run { Text = bbcode };
                }
                Inlines.Add(inline);
            }
            _dirty = false;
        }

        private void OnRequestNavigate(object sender, RequestNavigateEventArgs e) {
            try {
                // perform navigation using the link navigator
                LinkNavigator.Navigate(e.Uri, this, e.Target);
            } catch (Exception error) {
                // display navigation failures
                ModernDialog.ShowMessage(error.Message, ModernUI.Resources.NavigationFailed, MessageBoxButton.OK);
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
