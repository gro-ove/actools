using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Controls;
using AcManager.Pages.Windows;
using AcManager.Tools;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Pages.Miscellaneous {
    public partial class ModsWebBrowser : IParametrizedUriContent {
        private static ListViewModel Instance { get; } = new ListViewModel();

        public static void Initialize() {
            Instance.RebuildLinksNow();
        }

        public void OnUri(Uri uri) {
            var id = uri.GetQueryParam("Id");
            if (id == null) {
                DataContext = Instance;
            } else {
                _model = new ViewModel(Instance.WebSources.GetByIdOrDefault(id));
                DataContext = _model;
            }

            InitializeComponent();

            InputBindings.Add(new InputBinding(new DelegateCommand(() => OnScrollToSelectedButtonClick(null, null)),
                    new KeyGesture(Key.V, ModifierKeys.Control | ModifierKeys.Shift)));
            InputBindings.Add(new InputBinding(Instance.AddNewSourceCommand,
                    new KeyGesture(Key.N, ModifierKeys.Control | ModifierKeys.Shift)));
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs routedEventArgs) {
            var button = this.FindChild<Button>("ScrollToSelectedButton");
            if (button != null) {
                button.Click += OnScrollToSelectedButtonClick;
            }
        }

        private void OnScrollToSelectedButtonClick(object sender, RoutedEventArgs e) {
            var list = this.FindChild<ListBox>("SourcesList");
            list?.ScrollIntoView(list.SelectedItem);
        }

        [CanBeNull]
        private ViewModel _model;

        [JsonObject(MemberSerialization.OptIn)]
        public sealed class WebSource : NotifyPropertyChanged, IWithId {
            [JsonConstructor]
            private WebSource(string id) {
                Id = id;
            }

            public WebSource([CanBeNull] string id, [NotNull] string url) {
                Id = id ?? Guid.NewGuid().ToString();
                Name = url ?? throw new ArgumentNullException(nameof(url));

                if (!Regex.IsMatch(url, @"^https?://", RegexOptions.IgnoreCase)) {
                    url = @"http://" + url;
                }

                Url = url;
                Favicon = url.Split('/').Take(3).JoinToString('/').TrimEnd('/') + @"/favicon.ico";
                UpdateDisplayNameCommand.Execute();
                IsEnabled = true;
                IsFavourite = true;
            }

            [JsonProperty("id")]
            public string Id { get; }

            [JsonProperty("name")]
            private string _name;

            public string Name {
                get => _name;
                set => Apply(value, ref _name);
            }

            [JsonProperty("enabled")]
            private bool _isEnabled;

            public bool IsEnabled {
                get => _isEnabled;
                set => Apply(value, ref _isEnabled);
            }

            [JsonProperty("favourite")]
            private bool _isFavourite;

            public bool IsFavourite {
                get => _isFavourite;
                set => Apply(value, ref _isFavourite);
            }

            [JsonProperty("favicon")]
            private string _favicon;

            public string Favicon {
                get => _favicon;
                set => Apply(value, ref _favicon, () => Logging.Debug(value));
            }

            [JsonProperty("url")]
            private string _url;

            public string Url {
                get => _url;
                set => Apply(value, ref _url);
            }

            [JsonProperty("captureDownloads")]
            private bool _captureDownloads;

            public bool CaptureDownloads {
                get => _captureDownloads;
                set => Apply(value, ref _captureDownloads);
            }

            public string UserAgent => CmApiProvider.CommonUserAgent;

            private AsyncCommand _updateDisplayNameCommand;

            public AsyncCommand UpdateDisplayNameCommand => _updateDisplayNameCommand ?? (_updateDisplayNameCommand = new AsyncCommand(async () => {
                using (var wc = KillerOrder.Create(new CookieAwareWebClient(), TimeSpan.FromSeconds(10)))
                using (wc.Victim.SetUserAgent(UserAgent)) {
                    var data = await wc.Victim.DownloadStringTaskAsync(Url);

                    var url = wc.Victim.ResponseUri?.ToString();
                    if (!string.IsNullOrWhiteSpace(url)) {
                        Url = url;
                    }

                    var name = Regex.Match(data, @"<title>([^<]+)</title>").Groups[1].Value;
                    if (!string.IsNullOrWhiteSpace(name)) {
                        Name = name;
                    }

                    var favicon = Regex.Match(data, @"<link\s+rel=""icon""\s+href=""([^""]+)").Groups[1].Value;
                    if (!string.IsNullOrWhiteSpace(favicon)) {
                        Favicon = favicon;
                    } else if (!string.IsNullOrWhiteSpace(url)) {
                        Favicon = url.Split('/').Take(3).JoinToString('/').TrimEnd('/') + @"/favicon.ico";
                    }
                }
            }));

            private bool _isDeleted;

            public bool IsDeleted {
                get => _isDeleted;
                set => Apply(value, ref _isDeleted);
            }

            private DelegateCommand _deleteCommand;

            public DelegateCommand DeleteCommand => _deleteCommand ?? (_deleteCommand = new DelegateCommand(() => {
                if (ModernDialog.ShowMessage($"Delete bookmark to a website “{Name}”?", ControlsStrings.Common_AreYouSure, MessageBoxButton.YesNo)
                        != MessageBoxResult.Yes) return;
                IsDeleted = true;
            }));
        }

        private static readonly Lazier<IReadOnlyCollection<string>> FoundDomains = Lazier.CreateAsync(async () => {
            using (WaitingDialog.Create("Wait a second…"))
            using (var wc = KillerOrder.Create(new CookieAwareWebClient(), TimeSpan.FromSeconds(5)))
            using (wc.Victim.SetUserAgent(null)) {
                var data = await wc.Victim.DownloadStringTaskAsync("https://duckduckgo.com/html/?q=assetto+corsa+mods");
                return (IReadOnlyCollection<string>)Regex.Matches(data, @"result__a"" href=""([^""]+)""").OfType<Match>()
                                                         .Select(GetUrl).Where(SanityCheck).Take(10).ToList();
            }

            bool SanityCheck(string v) {
                return !new[] { 0, 1491093284, 518443847, 1564670876, 110165427 }.ArrayContains(-v?.GetHashCode() ?? 0);
            }

            string GetUrl(Match x) {
                var p = x.Groups[1].Value.Split(new[] { "uddg=" }, StringSplitOptions.RemoveEmptyEntries);
                var v = p.ArrayElementAtOrDefault(1)?.Split(new[] { "&amp;" }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                return string.IsNullOrWhiteSpace(v) ? null
                        : Regex.Replace(Uri.UnescapeDataString(v), @"^https?://(?:www\.)?|/$", "", RegexOptions.IgnoreCase);
            }
        });

        public class ListViewModel : NotifyPropertyChanged, IComparer<WebSource> {
            public ChangeableObservableCollection<WebSource> WebSources { get; }
            public BetterListCollectionView WebSourcesView { get; }

            private WebSource _selectedSource;

            public WebSource SelectedSource {
                get => _selectedSource;
                set => Apply(value, ref _selectedSource);
            }

            private readonly Storage _storage;

            public ListViewModel() {
                _storage = new Storage(FilesStorage.Instance.GetFilename("Sites.data"));
                WebSources = new ChangeableObservableCollection<WebSource>(_storage.Keys.Select(x => _storage.GetObject<WebSource>(x)).NonNull());
                WebSources.ItemPropertyChanged += OnItemPropertyChanged;
                WebSources.CollectionChanged += OnCollectionChanged;
                SelectedSource = WebSources.FirstOrDefault();

                WebSourcesView = new BetterListCollectionView(WebSources);
                WebSourcesView.SortDescriptions.Add(new SortDescription(nameof(WebSource.Name), ListSortDirection.Ascending));
            }

            private AsyncCommand _addNewSourceCommand;

            public AsyncCommand AddNewSourceCommand => _addNewSourceCommand ?? (_addNewSourceCommand = new AsyncCommand(async () => {
                var url = Prompt.Show("Enter URL for new source:", "Add new source", required: true, maxLength: 80, watermark: "?",
                        suggestions: await FoundDomains.GetValueAsync(),
                        comment: $"Suggestions are provided by [url={BbCodeBlock.EncodeAttribute("https://duckduckgo.com/")}]DuckDuckGo[/url] search engine. "
                                + "Content Manager doesn’t encourage you to use any of them.");
                if (url == null) return;

                var newSource = new WebSource(null, url);
                WebSources.AddSorted(newSource, this);
                SelectedSource = newSource;
            }));

            private readonly Busy _rebuildLinksBusy = new Busy();

            private void RebuildLinks() {
                _rebuildLinksBusy.Yield(RebuildLinksNow);
            }

            public void RebuildLinksNow() {
                var mainWindow = Application.Current.Windows.OfType<MainWindow>().FirstOrDefault();
                if (mainWindow == null) return;

                var linkGroup = mainWindow.BrowserLinkGroup;
                foreach (var link in linkGroup.Links.Skip(1).ToList()) {
                    linkGroup.Links.Remove(link);
                }

                _storage.CleanUp(x => true);
                foreach (WebSource source in WebSourcesView) {
                    if (source.IsEnabled && source.IsFavourite) {
                        linkGroup.Links.Add(new Link {
                            DisplayName = source.Name,
                            Icon = source.Favicon == null ? null : new BetterImage { Filename = source.Favicon },
                            Source = new Uri("/Pages/Miscellaneous/ModsWebBrowser.xaml", UriKind.Relative).AddQueryParam("Id", source.Id)
                        });
                        /*linkGroup.Links.Add(new LinkInput(new Uri("/Pages/Miscellaneous/ModsWebBrowser.xaml", UriKind.Relative), "") {
                            DisplayName = source.Name,
                            Icon = source.Favicon == null ? null : new BetterImage { Filename = source.Favicon },
                            Source = new Uri("/Pages/Miscellaneous/ModsWebBrowser.xaml", UriKind.Relative).AddQueryParam("Id", source.Id)
                        });*/
                    }

                    _storage.SetObject(source.Id, source);
                }
            }

            private void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e) {
                if (e.PropertyName == nameof(WebSource.IsDeleted)) {
                    WebSources.Remove((WebSource)sender);
                } else {
                    RebuildLinks();
                }
            }

            private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
                RebuildLinks();
            }

            public int Compare(WebSource x, WebSource y) {
                return x?.Name?.CompareTo(y?.Name) ?? 0;
            }
        }

        public class ViewModel : NotifyPropertyChanged {
            public WebSource Source { get; }

            public ViewModel(WebSource source) {
                Source = source;
            }
        }
    }
}