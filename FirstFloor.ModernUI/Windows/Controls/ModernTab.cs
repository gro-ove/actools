using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Media;
using FirstFloor.ModernUI.Windows.Navigation;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Windows.Controls {
    public interface ITitleable {
        string Title { get; }
    }

    public enum SavePolicy {
        /// <summary>
        /// Save or load only when URI is in one of original links.
        /// </summary>
        Strict,

        /// <summary>
        /// Save or load even if there is no link with that URI.
        /// </summary>
        Flexible,

        /// <summary>
        /// Do not load URI, but instead use current Source value.
        /// </summary>
        SkipLoading,

        /// <summary>
        /// Do not load URI, but instead use current Source value, even if there is no link with that URI.
        /// </summary>
        SkipLoadingFlexible
    }

    public class DirectContentLoaderEntry {
        public Uri Source { get; set; }

        public string Key {
            get => Source.OriginalString;
            set => Source = new Uri(value, UriKind.Relative);
        }

        public object Content { get; set; }
    }

    [ContentProperty(nameof(Entries))]
    public class DirectContentLoader : IContentLoader {
        public List<DirectContentLoaderEntry> Entries { get; }

        public DirectContentLoader() {
            Entries = new List<DirectContentLoaderEntry>(3);
        }

        public Task<object> LoadContentAsync(Uri uri, CancellationToken cancellationToken) {
            return Task.FromResult(LoadContent(uri));
        }

        public object LoadContent(Uri uri) {
            return Entries.FirstOrDefault(x => string.Equals(x.Source.ToString(), uri.OriginalString, StringComparison.OrdinalIgnoreCase))?.Content;
        }
    }

    public class ModernTab : Control {
        public static readonly DependencyProperty LinksHorizontalAlignmentProperty = DependencyProperty.Register("LinksHorizontalAlignment",
                typeof(HorizontalAlignment), typeof(ModernTab), new PropertyMetadata());

        public HorizontalAlignment LinksHorizontalAlignment {
            get => GetValue(LinksHorizontalAlignmentProperty) as HorizontalAlignment? ?? default;
            set => SetValue(LinksHorizontalAlignmentProperty, value);
        }

        public static readonly DependencyProperty LinksMarginProperty = DependencyProperty.Register("LinksMargin", typeof(Thickness),
                typeof(ModernTab), new PropertyMetadata(new Thickness(0.0, 0.0, 0.0, 0.0)));

        public static readonly DependencyProperty FrameMarginProperty = DependencyProperty.Register("FrameMargin", typeof(Thickness),
                typeof(ModernTab), new PropertyMetadata(new Thickness(0.0, 0.0, 0.0, 0.0)));

        public Thickness LinksMargin {
            get => GetValue(LinksMarginProperty) as Thickness? ?? default;
            set => SetValue(LinksMarginProperty, value);
        }

        public Thickness FrameMargin {
            get => GetValue(FrameMarginProperty) as Thickness? ?? default;
            set => SetValue(FrameMarginProperty, value);
        }

        public static readonly DependencyProperty SavePolicyProperty = DependencyProperty.Register(nameof(SavePolicy), typeof(SavePolicy),
                typeof(ModernTab));

        public SavePolicy SavePolicy {
            get => GetValue(SavePolicyProperty) as SavePolicy? ?? default;
            set => SetValue(SavePolicyProperty, value);
        }

        public static readonly DependencyProperty ContentLoaderProperty = DependencyProperty.Register("ContentLoader", typeof(IContentLoader),
                typeof(ModernTab), new PropertyMetadata(new DefaultContentLoader()));

        public static readonly DependencyProperty LayoutProperty = DependencyProperty.Register("Layout", typeof(TabLayout),
                typeof(ModernTab), new PropertyMetadata(TabLayout.Tab));

        public static readonly DependencyProperty ListWidthProperty = DependencyProperty.Register("ListWidth", typeof(GridLength),
                typeof(ModernTab), new PropertyMetadata(new GridLength(170)));

        public static readonly DependencyProperty LinksProperty = DependencyProperty.Register("Links", typeof(LinkCollection),
                typeof(ModernTab), new PropertyMetadata(OnLinksChanged));

        public static readonly DependencyProperty SelectedSourceProperty = DependencyProperty.Register("SelectedSource", typeof(Uri),
                typeof(ModernTab), new PropertyMetadata(OnSelectedSourceChanged));

        public event EventHandler<SourceEventArgs> SelectedSourceChanged;
        public event EventHandler<NavigationEventArgs> FrameNavigated;

        private ListBox _linkList;

        private void SavePinned() {
            if (SaveKey != null) {
                ValuesStorage.Storage.SetStringList(SaveKey + "/pinned",
                        PinnedLinks.Select(x => Storage.EncodeList(x.DisplayName, x.Source?.OriginalString ?? "")));
            }
        }

        private void LoadPinned() {
            if (SaveKey != null) {
                PinnedLinks.Clear();
                foreach (var item in ValuesStorage.Storage.GetStringList(SaveKey + "/pinned").Select(Storage.DecodeList)) {
                    var values = item.ToList();
                    if (values.Count == 2) {
                        PinnedLinks.Add(new Link {
                            DisplayName = values[0],
                            Source = new Uri(values[1], UriKind.Relative),
                            IsPinned = true
                        });
                    }
                }
                foreach (var link in PinnedLinks) {
                    link.PropertyChanged += OnPinnedLinkChanged;
                }
            }
        }

        public ModernTab() {
            DefaultStyleKey = typeof(ModernTab);
            SetCurrentValue(LinksProperty, new LinkCollection());
            SetCurrentValue(PinnedLinksProperty, new LinkCollection());
            SetCurrentValue(PinCurrentCommandProperty, new DelegateCommand(async () => {
                if (Title == null || SelectedSource == null || _linkList == null) return;
                var link = new Link {
                    DisplayName = Title,
                    Source = SelectedSource,
                    IsPinned = true
                };
                Title = null;
                await Task.Yield();
                PinnedLinks.Add(link);
                _linkList.SelectedItem = link;
                link.PropertyChanged += OnPinnedLinkChanged;
                SavePinned();
            }));
        }

        private void OnPinnedLinkChanged(object sender, PropertyChangedEventArgs args) {
            var link = (Link)sender;
            if (!link.IsPinned) {
                var reselect = SelectedSource == link.Source;
                PinnedLinks.Remove(link);
                if (reselect) {
                    Title = link.DisplayName;
                    _linkList.SelectedItem = null;
                }
                SavePinned();
            }
        }

        public ModernFrame Frame { get; private set; }

        private static void OnLinksChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((ModernTab)o).UpdateSelection(false);
        }

        private static void OnSelectedSourceChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((ModernTab)o).OnSelectedSourceChanged((Uri)e.NewValue);
        }

        private void OnSelectedSourceChanged(Uri newValue) {
            UpdateSelection(true);
            SelectedSourceChanged?.Invoke(this, new SourceEventArgs(newValue));
        }

        private void UpdateSelection(bool skipLoading) {
            if (_linkList == null || Links == null || SavePolicy == SavePolicy.SkipLoadingFlexible) {
                return;
            }

            if (SavePolicy == SavePolicy.SkipLoading && Frame.Source != null) {
                _linkList.SelectedItem = Links.FirstOrDefault(l => l.IsShown && l.Source == SelectedSource)
                        ?? PinnedLinks.FirstOrDefault(l => l.IsShown && l.Source == SelectedSource)
                                ?? (skipLoading ? null : Links.FirstOrDefault());
                return;
            }

            if (!skipLoading && SavePolicy == SavePolicy.Flexible && SaveKey != null) {
                Frame.Source = ValuesStorage.Get<Uri>(SaveKey) ?? Links.FirstOrDefault()?.Source;
            } else {
                var saved = skipLoading || SaveKey == null ? null : ValuesStorage.Get<string>(SaveKey);
                _linkList.SelectedItem = (saved == null ? null
                        : Links.FirstOrDefault(l => l.IsShown && l.Source?.OriginalString == saved)
                                ?? PinnedLinks.FirstOrDefault(l => l.IsShown && l.Source?.OriginalString == saved))
                        ?? Links.FirstOrDefault(l => l.IsShown && l.Source == SelectedSource)
                                ?? PinnedLinks.FirstOrDefault(l => l.IsShown && l.Source == SelectedSource)
                                        ?? (skipLoading ? null : Links.FirstOrDefault());
            }
        }

        public override void OnApplyTemplate() {
            base.OnApplyTemplate();

            if (_linkList != null) {
                _linkList.SelectionChanged -= OnLinkListSelectionChanged;
            }

            if (Frame != null) {
                Frame.Navigated -= Frame_Navigated;
            }

            _linkList = GetTemplateChild(@"PART_LinkList") as ListBox;
            Frame = GetTemplateChild(@"PART_Frame") as ModernFrame;

            if (_linkList != null) {
                _linkList.SelectionChanged += OnLinkListSelectionChanged;
            }

            if (Frame != null) {
                Frame.Navigated += Frame_Navigated;
            }

            UpdateSelection(false);
        }

        public double? GetLinkListWidth() {
            return _linkList?.ActualWidth;
        }

        private void OnLinkListSelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (_linkList.SelectedItem is Link link && link.Source != SelectedSource) {
                SetCurrentValue(SelectedSourceProperty, link.Source);
            }
        }

        private void Frame_Navigated(object sender, NavigationEventArgs navigationEventArgs) {
            if (Layout == TabLayout.TabWithTitle) {
                Title = PinnedLinks.Any(x => x.Source == SelectedSource) ? null : (Frame.Content as ITitleable)?.Title;
            }

            FrameNavigated?.Invoke(this, navigationEventArgs);
            if (SaveKey != null && (_linkList.SelectedItem != null || SavePolicy == SavePolicy.Flexible)) {
                ValuesStorage.Set(SaveKey, Frame.Source.OriginalString);
            }
        }

        public IContentLoader ContentLoader {
            get => (IContentLoader)GetValue(ContentLoaderProperty);
            set => SetValue(ContentLoaderProperty, value);
        }

        public TabLayout Layout {
            get => GetValue(LayoutProperty) as TabLayout? ?? default;
            set {
                Title = null;
                SetValue(LayoutProperty, value);
            }
        }

        public LinkCollection Links {
            get => (LinkCollection)GetValue(LinksProperty);
            set => SetValue(LinksProperty, value);
        }

        public static readonly DependencyProperty PinnedLinksProperty = DependencyProperty.Register(nameof(PinnedLinks), typeof(LinkCollection),
                typeof(ModernTab));

        public LinkCollection PinnedLinks {
            get => (LinkCollection)GetValue(PinnedLinksProperty);
            set => SetValue(PinnedLinksProperty, value);
        }

        public GridLength ListWidth {
            get => GetValue(ListWidthProperty) as GridLength? ?? default;
            set => SetValue(ListWidthProperty, value);
        }

        public Uri SelectedSource {
            get => (Uri)GetValue(SelectedSourceProperty);
            set => SetValue(SelectedSourceProperty, value);
        }

        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string),
                typeof(ModernTab));

        public string Title {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly DependencyProperty PinCurrentCommandProperty = DependencyProperty.Register(nameof(PinCurrentCommand), typeof(ICommand),
                typeof(ModernTab));

        public ICommand PinCurrentCommand {
            get => (ICommand)GetValue(PinCurrentCommandProperty);
            set => SetValue(PinCurrentCommandProperty, value);
        }

        // saving and loading uri
        public static readonly DependencyProperty SaveKeyProperty = DependencyProperty.Register(nameof(SaveKey), typeof(string),
                typeof(ModernTab), new PropertyMetadata(OnSaveKeyChanged));

        [CanBeNull]
        public string SaveKey {
            get => (string)GetValue(SaveKeyProperty);
            set => SetValue(SaveKeyProperty, value);
        }

        private static void OnSaveKeyChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) {
            ((ModernTab)o).LoadPinned();
            ((ModernTab)o).UpdateSelection(false);
        }

        public static readonly DependencyProperty LinksListBoxTemplateProperty = DependencyProperty.Register(nameof(LinksListBoxTemplate),
                typeof(ControlTemplate),
                typeof(ModernTab));

        public ControlTemplate LinksListBoxTemplate {
            get => (ControlTemplate)GetValue(LinksListBoxTemplateProperty);
            set => SetValue(LinksListBoxTemplateProperty, value);
        }
    }

    public class ModernTabDataTemplateSelector : DataTemplateSelector {
        public DataTemplate LinkDataTemplate { get; set; }

        public DataTemplate PinnedLinkDataTemplate { get; set; }

        public DataTemplate TitleDataTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container) {
            return item is Link l ? l.IsPinned ? PinnedLinkDataTemplate : LinkDataTemplate : TitleDataTemplate;
        }
    }

    public class ModernTabDataLinkListTemplateSelector : DataTemplateSelector {
        public DataTemplate ListLinkDataTemplate { get; set; }

        public DataTemplate LinkDataTemplate { get; set; }

        public override DataTemplate SelectTemplate(object item, DependencyObject container) {
            return item is LinksList ? ListLinkDataTemplate : LinkDataTemplate;
        }
    }

    public class ModernTabLinksComboBox : BetterComboBox {
        public static readonly DependencyProperty IsAnySelectedProperty = DependencyProperty.Register(nameof(IsAnySelected), typeof(bool),
                typeof(ModernTabLinksComboBox), new PropertyMetadata(false, (o, e) => {
                    ((ModernTabLinksComboBox)o)._isAnySelected = (bool)e.NewValue;
                }));

        private bool _isAnySelected;

        public bool IsAnySelected {
            get => _isAnySelected;
            set => SetValue(IsAnySelectedProperty, value);
        }
        
        private ModernTab _parent;
        
        public ModernTabLinksComboBox() {
            PreviewMouseUp += (sender, args) => {
                if (_parent == null) return;
                var popup = this.FindVisualChild<Popup>();
                if (popup?.IsOpen == true) {
                    popup.IsOpen = false;
                    _parent.SelectedSource = (SelectedItem as Link)?.Source;
                }
            };
            Loaded += (sender, args) => {
                _parent = this.GetParent<ModernTab>();
                if (_parent == null) return;
                _parent.SelectedSourceChanged += OnParentSelectedSourceChanged;
            };
            Unloaded += (sender, args) => {
                if (_parent == null) return;
                _parent.SelectedSourceChanged -= OnParentSelectedSourceChanged;
            };
        }

        private void OnParentSelectedSourceChanged(object o, SourceEventArgs eventArgs) {
            var selected = ItemsSource.OfType<Link>().FirstOrDefault(x => x.Source == eventArgs.Source);
            if (selected != null && SelectedItem != selected) {
                SelectedItem = selected;
            }
            IsAnySelected = selected != null;
        }

        protected override void OnSelectionChanged(SelectionChangedEventArgs e) {
            base.OnSelectionChanged(e);
            if (SelectedItem is Link selected && _parent != null && _parent.SelectedSource != selected.Source) {
                _parent.SelectedSource = selected.Source;
            }
        }
    }
}