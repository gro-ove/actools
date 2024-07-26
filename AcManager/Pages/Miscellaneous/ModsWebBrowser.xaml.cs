using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Controls;
using AcManager.Controls.Helpers;
using AcManager.Controls.Presentation;
using AcManager.Controls.UserControls;
using AcManager.Controls.UserControls.Web;
using AcManager.Internal;
using AcManager.Pages.Windows;
using AcManager.Tools;
using AcManager.Tools.ContentInstallation;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Helpers.Loaders;
using AcManager.Tools.Managers.Plugins;
using AcManager.UserControls;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Serialization;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Navigation;
using HtmlAgilityPack;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.Pages.Miscellaneous {
    public partial class ModsWebBrowser : IParametrizedUriContent {
        public static ListViewModel Instance { get; private set; }

        public static void Initialize() {
            Instance = new ListViewModel();
            WebBlock.NewTabGlobal += OnNewTabGlobal;
        }

        private static void OnNewTabGlobal(object o, NewWindowEventArgs args) {
            if (SettingsHolder.WebBlocks.CaptureViaFileStorageLoaders && FlexibleLoader.IsSupportedFileStorage(args.Url)) {
                args.Cancel = true;
                ContentInstallationManager.Instance.InstallAsync(args.Url, ContentInstallationParams.DefaultWithExecutables);
                return;
            }

            var loader = GetSource(args.Url, out var isVirtual);
            if (isVirtual || loader?.CaptureRedirects != true) return;

            args.Cancel = true;
            ContentInstallationManager.Instance.InstallAsync(args.Url, ContentInstallationParams.DefaultWithExecutables);
        }

        public void OnUri(Uri uri) {
            var id = uri.GetQueryParam("Id");
            if (id == null) {
                DataContext = Instance;
            } else {
                _model = new ViewModel(Instance.WebSources.GetById(id));
                DataContext = _model;
            }

            InitializeComponent();
            InputBindings.Add(new InputBinding(new DelegateCommand(() => OnScrollToSelectedButtonClick(null, null)),
                    new KeyGesture(Key.V, ModifierKeys.Control | ModifierKeys.Shift)));
            InputBindings.Add(new InputBinding(Instance.AddNewSourceCommand,
                    new KeyGesture(Key.N, ModifierKeys.Control | ModifierKeys.Shift)));
            Loaded += OnLoaded;
        }

        private void OnWebBlockLoaded(object sender, RoutedEventArgs e) {
            ((WebBlock)sender).SetJsBridge<CheckingJsBridge>();
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

        private const string SuggestedRulesKey = ".ModsWebBrowser.Suggestions";
        private const string SuggestedRulesDateKey = ".ModsWebBrowser.SuggestionsDate";
        private static readonly TaskCache SuggestedRulesTaskCache = new TaskCache();
        private static WebDownloadRule[] _suggestedRules;

        [NotNull, ItemNotNull]
        private static Task<IReadOnlyList<WebDownloadRule>> GetSuggestionsAsync() {
            if (_suggestedRules != null) {
                return Task.FromResult<IReadOnlyList<WebDownloadRule>>(_suggestedRules);
            }

            return SuggestedRulesTaskCache.Get(() => Task.Run<IReadOnlyList<WebDownloadRule>>(() => {
                var now = DateTime.Now;
                if ((now - CacheStorage.Get<DateTime>(SuggestedRulesDateKey)).TotalHours < 24) {
                    _suggestedRules = CacheStorage.Storage.GetObject<WebDownloadRule[]>(SuggestedRulesKey) ?? new WebDownloadRule[0];
                    Logging.Write("Loaded from cache: " + _suggestedRules.Length);
                    return _suggestedRules;
                }

                try {
                    _suggestedRules = InternalUtils.LoadWebDownloadRules(CmApiProvider.UserAgent).ToArray();
                } catch (Exception e) {
                    Logging.Warning(e.Message);
                    _suggestedRules = new WebDownloadRule[0];
                }

                CacheStorage.Storage.Set(SuggestedRulesDateKey, now);
                CacheStorage.Storage.SetObject(SuggestedRulesKey, _suggestedRules);
                Logging.Write("Actual data loaded: " + _suggestedRules.Length);
                return _suggestedRules;
            }));
        }

        [NotNull, ItemNotNull]
        private static async Task<IEnumerable<WebDownloadRule>> GetSuggestionsAsync([NotNull] string url) {
            var domain = url.GetDomainNameFromUrl();
            return (await GetSuggestionsAsync().ConfigureAwait(false)).Where(x => x.Domain == domain);
        }

        [NotNull, ItemCanBeNull]
        private static async Task<WebDownloadRule> GetSuggestionAsync([NotNull] string url) {
            var domain = url.GetDomainNameFromUrl();
            return (await GetSuggestionsAsync().ConfigureAwait(false)).FirstOrDefault(x => x.Domain == domain);
        }

        [CanBeNull]
        private ViewModel _model;

        public enum WebSourceSerializationMode {
            Full,
            Compact
        }

        [JsonObject(MemberSerialization.OptIn)]
        public sealed class WebSource : NotifyPropertyChanged, IWithId, ILinkNavigator, IComparable {
            [JsonConstructor]
            private WebSource(string id) {
                Id = id;
                RuleSuggestions = Lazier.CreateAsync(() => GetSuggestionsAsync(Url).ContinueWith(
                        t => t.Result.Select(x => x.Rule).ToIReadOnlyListIfItIsNot(),
                        TaskContinuationOptions.OnlyOnRanToCompletion));
                RuleSuggestions.PropertyChanged += OnPropertyChanged;
                UpdateRedirectsToNames();
            }

            private void OnPropertyChanged(object sender, PropertyChangedEventArgs args) {
                if (args.PropertyName == nameof(RuleSuggestions.Value) && RuleSuggestions.Value != null && _autoSetRule) {
                    AutoDownloadRule = RuleSuggestions.Value.FirstOrDefault();
                }
            }

            public WebSource([CanBeNull] string id, [NotNull] string url) : this(id ?? Guid.NewGuid().ToString()) {
                Name = url ?? throw new ArgumentNullException(nameof(url));

                if (!Regex.IsMatch(url, @"^https?://", RegexOptions.IgnoreCase)) {
                    url = @"http://" + url;
                }

                _autoSetRule = true;
                Url = url;
                Favicon = url.GetWebsiteFromUrl() + @"/favicon.ico";
            }

            private bool _autoSetRule;

            [JsonProperty("id")]
            public string Id { get; }

            [JsonProperty("name")]
            private string _name;

            [CanBeNull]
            public string Name {
                get => _name;
                set => Apply(value, ref _name);
            }

            [JsonProperty("enabled")]
            private bool _isEnabled = true;

            public bool IsEnabled {
                get => _isEnabled;
                set => Apply(value, ref _isEnabled);
            }

            [JsonProperty("favourite")]
            private bool _isFavourite = true;

            public bool IsFavourite {
                get => _isFavourite;
                set => Apply(value, ref _isFavourite);
            }

            [JsonProperty("favicon")]
            private string _favicon;

            [CanBeNull]
            public string Favicon {
                get => _favicon;
                set => Apply(value, ref _favicon, () => Logging.Debug(value));
            }

            [JsonProperty("url")]
            private string _url;

            [NotNull]
            public string Url {
                get => _url ?? "";
                set => Apply(value, ref _url, () => RuleSuggestions.Reset());
            }

            [JsonProperty("downloadsCapture")]
            private bool _captureDownloads = true;

            public bool CaptureDownloads {
                get => _captureDownloads;
                set => Apply(value, ref _captureDownloads);
            }

            [JsonProperty("redirectsCapture")]
            private bool _captureRedirects;

            public bool CaptureRedirects {
                get => _captureRedirects;
                set => Apply(value, ref _captureRedirects);
            }

            [JsonProperty("downloadsRule")]
            private string _autoDownloadRule;

            [CanBeNull]
            public string AutoDownloadRule {
                get => _autoDownloadRule;
                set => Apply(value, ref _autoDownloadRule, () => _autoSetRule = false);
            }

            [NotNull, JsonProperty("redirectsTo")]
            public List<string> RedirectsTo { get; } = new List<string>();

            public void AddRedirectsTo(string id) {
                if (string.IsNullOrWhiteSpace(id) || RedirectsTo.FirstOrDefault() == id) return;
                if (RedirectsTo.Remove(id)) {
                    RedirectsTo.Insert(0, id);
                } else {
                    RedirectsTo.Add(id);
                }

                UpdateRedirectsToNames();
                OnPropertyChanged(nameof(RedirectsTo));
            }

            public BetterObservableCollection<string> RedirectsToNames { get; } = new BetterObservableCollection<string>();

            private Busy _redirectsToNamesBusy = new Busy();

            private void UpdateRedirectsToNames() {
                if (Instance != null) {
                    _redirectsToNamesBusy.Yield(() => RedirectsToNames.ReplaceEverythingBy_Direct(
                            RedirectsTo.Select(x => Instance.WebSources.GetByIdOrDefault(x)?.Name).NonNull()));
                }
            }

            private static readonly string ScriptPrefix = @"javascript:";

            public static bool IsScriptRule(string rule) {
                return rule.StartsWith(ScriptPrefix, StringComparison.OrdinalIgnoreCase);
            }

            public static string CompactRule(string rule) {
                if (IsScriptRule(rule)) return rule;

                // Cut out spaces for CSS selector
                var wrapped = rule.WrapQuoted(out var unwrap);
                return unwrap(Regex.Replace(wrapped, @"\s+", ""));
            }

            public static string GetActionScript(string rule) {
                var code = IsScriptRule(rule)
                        ? $@"eval({JsonConvert.SerializeObject(rule.ApartFromFirst(ScriptPrefix, StringComparison.OrdinalIgnoreCase))});"
                        : $@"document.querySelector({JsonConvert.SerializeObject(rule)}).click();";
                return @"
window.$PAUSEKEY = true;
var foundCallback = false;
function downloadCallback(v){ window.external.DownloadFrom(v) }
try { $CODE } catch (e){ console.warn(e) }".Replace(@"$CODE", code)
                        .Replace(@"$PAUSEKEY", PauseKey);
            }

            public static string GetCheckScript(string rule) {
                var code = IsScriptRule(rule)
                        ? $@"eval({JsonConvert.SerializeObject(rule.ApartFromFirst(ScriptPrefix, StringComparison.OrdinalIgnoreCase))});"
                        : $@"foundCallback(document.querySelector({JsonConvert.SerializeObject(rule)}) != null);";
                return @"
function foundCallback(v){ window.external.Callback(v) }
var downloadCallback = false;
try { $CODE } catch (e){ console.warn(e) }".Replace(@"$CODE", code);
            }

            public static bool IsRuleSafe(string rule) {
                return !IsScriptRule(rule);
            }

            public Lazier<IReadOnlyList<string>> RuleSuggestions { get; }

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

                    var doc = new HtmlDocument();
                    doc.LoadHtml(data);

                    var name = HttpUtility.HtmlDecode(doc.DocumentNode.Descendants(@"title")?.FirstOrDefault()?.InnerText.Trim());
                    if (!string.IsNullOrWhiteSpace(name)) {
                        Name = name;
                    }

                    var favicon = doc.DocumentNode.Descendants(@"link")?.Select(x => new {
                        Role = new[] {
                            @"apple-touch-icon",
                            @"apple-touch-icon-precomposed",
                            @"icon",
                            @"shortcut icon",
                        }.IndexOf(x.Attributes[@"rel"]?.Value?.Trim().ToLowerInvariant()),
                        Url = x.Attributes[@"href"]?.Value,
                        Size = Regex.Matches(x.Attributes[@"sizes"]?.Value ?? @"0", @"\d+")
                                .Cast<Match>().Select(y => y.Value.As<int>()).MaxOrDefault()
                    }).Where(x => x.Role != -1 && x.Url != null).OrderByDescending(x => x.Size).ThenBy(x => x.Role).FirstOrDefault()?.Url;
                    if (!string.IsNullOrWhiteSpace(favicon)) {
                        Favicon = GetFullPath(favicon, url);
                    } else if (!string.IsNullOrWhiteSpace(url)) {
                        Favicon = url.GetWebsiteFromUrl() + @"/favicon.ico";
                    }
                }

                string GetFullPath(string url, string webpageUrl, Func<string> baseUrlCallback = null) {
                    if (url.IsWebUrl()) return url;
                    if (url.StartsWith(@"/")) return webpageUrl.GetWebsiteFromUrl() + url;
                    var baseUrl = baseUrlCallback?.Invoke() ?? Regex.Replace(webpageUrl, @"(?<=\w/)[^/]*(?:[?#].*)?$", "", RegexOptions.IgnoreCase);
                    return (baseUrl.EndsWith(@"/") ? baseUrl : baseUrl + @"/") + url;
                }
            }));

            private AsyncCommand _setGoogleFaviconCommand;

            public AsyncCommand SetGoogleFaviconCommand => _setGoogleFaviconCommand ?? (_setGoogleFaviconCommand = new AsyncCommand(
                    async () => Favicon = await FaviconProvider.GetFaviconAsync(Url)));

            private bool _isDeleted;

            public bool IsDeleted {
                get => _isDeleted;
                set => Apply(value, ref _isDeleted);
            }

            private DelegateCommand _deleteCommand;

            public DelegateCommand DeleteCommand => _deleteCommand ?? (_deleteCommand = new DelegateCommand(() => {
                if (ModernDialog.ShowMessage(string.Format(AppStrings.WebSource_DeleteBookmark, Name), ControlsStrings.Common_AreYouSure, MessageBoxButton.YesNo)
                        != MessageBoxResult.Yes) return;
                WebBlock.Unload($@"ModsWebBrowser:{Id}");
                IsDeleted = true;
            }));

            private DelegateCommand _shareLinkCommand;

            public DelegateCommand ShareLinkCommand => _shareLinkCommand ?? (_shareLinkCommand = new DelegateCommand(() => {
                var link = ExportAsLink(true);
                if (link == null) {
                    NonfatalError.Notify(AppStrings.WebSource_FailedToShareTitle, AppStrings.WebSource_FailedToShareMessage);
                    return;
                }

                SharingUiHelper.ShowShared(string.Format(AppStrings.WebSource_SharedDescriptionTitle, Name), link, true);
            }));

            private DelegateCommand _shareMarkdownCommand;

            public DelegateCommand ShareMarkdownCommand => _shareMarkdownCommand ?? (_shareMarkdownCommand = new DelegateCommand(() => {
                var link = ExportAsLink(true);
                if (link == null) {
                    NonfatalError.Notify(AppStrings.WebSource_FailedToShareTitle, AppStrings.WebSource_FailedToShareMessage);
                    return;
                }

                var piece = $@"### [![Icon]({FaviconProvider.GetFaviconAsync(Url).Result}) {Name}]({Url})

```{(IsScriptRule(AutoDownloadRule) ? @"javascript" : @"css")}
{AutoDownloadRule}
```

- [Import settings]({link}).";
                ClipboardHelper.SetText(piece);
                Toast.Show(string.Format(AppStrings.WebSource_SharedDescriptionTitle, Name),
                        AppStrings.WebSource_SharedMessageCopiedTitle,
                        () => WindowsHelper.ViewInBrowser(@"https://acstuff.club/f/d/24-content-manager-websites-with-mods"));
            }));

            [CanBeNull]
            private string ExportAsLink(bool single) {
                foreach (var mode in new[] { WebSourceSerializationMode.Full, WebSourceSerializationMode.Compact }) {
                    var link = $@"{InternalUtils.MainApiDomain}/s/q:importWebsite?";
                    var count = 0;
                    foreach (var source in single ? new[] { this } : WithChildren(false)) {
                        var p = source.Serialize(mode);
                        Logging.Write($"Serialized, length: {p?.Length}");
                        if (p != null && link.Length + p.Length < 1600) {
                            if (!link.EndsWith(@"?")) link += @"&";
                            link += $@"data={Uri.EscapeDataString(p)}";
                        } else if (count == 0) break;

                        count++;
                    }

                    if (count > 0) {
                        return link;
                    }
                }

                return null;
            }

            public IEnumerable<WebSource> WithChildren(bool safeOnly) {
                return IsRuleSafe(AutoDownloadRule) ? ListSources(this, new List<string>()) : new WebSource[0];

                IEnumerable<WebSource> ListSources(WebSource from, ICollection<string> returned) {
                    returned.Add(from.Id);
                    return from.RedirectsTo
                            .Where(x => !returned.Contains(x))
                            .Select(x => Instance.WebSources.GetByIdOrDefault(x) ?? VirtualSources.GetByIdOrDefault(x))
                            .Where(x => x != null && (!safeOnly || IsRuleSafe(x.AutoDownloadRule)))
                            .SelectMany(x => ListSources(x, returned)).Prepend(from);
                }
            }

            public string Serialize(WebSourceSerializationMode mode, string referenceUrl = null) {
                if (mode == WebSourceSerializationMode.Compact) {
                    return referenceUrl?.GetWebsiteFromUrl() == Url.GetWebsiteFromUrl()
                            ? Combine(CompactRule(AutoDownloadRule))
                            : Combine(Url, CompactRule(AutoDownloadRule));
                }

                return Favicon == FaviconProvider.GetFaviconAsync(Url).Result
                        ? Combine(Url, Name, AutoDownloadRule)
                        : Combine(Url, Name, Favicon, AutoDownloadRule);

                byte[] Compress(byte[] data) {
                    if (data == null) return null;
                    try {
                        using (var memory = new MemoryStream()) {
                            using (var deflate = new DeflateStream(memory, CompressionMode.Compress)) {
                                deflate.Write(data, 0, data.Length);
                            }

                            return memory.ToArray();
                        }
                    } catch (Exception e) {
                        Logging.Error(e);
                        return null;
                    }
                }

                string Combine(params string[] values) {
                    var d = values.Select(x => Storage.Encode(x ?? "")).JoinToString('\n');
                    return Compress(Encoding.UTF8.GetBytes(d))?.ToCutBase64();
                }
            }

            [CanBeNull]
            public static WebSource Deserialize([NotNull] string websiteData, [CanBeNull] string targetUrl = null) {
                return Deserialize(new[] { websiteData }, new[] { targetUrl }).FirstOrDefault();
            }

            [NotNull]
            public static IEnumerable<WebSource> Deserialize([NotNull] string[] websiteData, [CanBeNull] string[] targetUrls = null) {
                var urlsWebSites = targetUrls?.Select(x => x.GetWebsiteFromUrl()?.ToLowerInvariant()).NonNull().Distinct().ToList();
                foreach (var piece in websiteData) {
                    var s = Decompress(piece.FromCutBase64())?.ToUtf8String().Split('\n').Select(Storage.Decode).ToArray();
                    string url = s?.FirstOrDefault(), name = null, favicon = null, autoDownloadRule = s?.LastOrDefault();
                    switch (s?.Length) {
                        case 1:
                            // Auto-download rule
                            if (urlsWebSites?.Count != 1) continue;
                            url = urlsWebSites[0];
                            break;
                        case 2:
                            // URL, auto-download rule
                            break;
                        case 3:
                            // URL, name, auto-download rule
                            name = s[1];
                            break;
                        case 4:
                            // URL, name, favicon, auto-download rule
                            name = s[1];
                            favicon = s[2];
                            break;
                        default:
                            Logging.Warning("Wrong number of pieces: "
                                    + (s == null ? @"NULL" : s.Length + Environment.NewLine + s.JoinToString(Environment.NewLine)));
                            continue;
                    }

                    if (string.IsNullOrWhiteSpace(url)) {
                        Logging.Warning("URL is missing");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(autoDownloadRule)) {
                        autoDownloadRule = null;
                    }

                    if (string.IsNullOrWhiteSpace(name)) {
                        name = url;
                    }

                    if (string.IsNullOrWhiteSpace(favicon)) {
                        favicon = FaviconProvider.GetFaviconAsync(url).Result;
                    }

                    yield return new WebSource(null, url) {
                        Name = name,
                        Favicon = favicon,
                        AutoDownloadRule = autoDownloadRule,
                        IsEnabled = true,
                        CaptureDownloads = true,
                    };

                    byte[] Decompress(byte[] data) {
                        if (data == null) return null;
                        using (var input = new MemoryStream(data))
                        using (var output = new MemoryStream()) {
                            using (var dstream = new DeflateStream(input, CompressionMode.Decompress)) {
                                dstream.CopyTo(output);
                            }

                            return output.ToArray();
                        }
                    }
                }
            }

            CommandDictionary ILinkNavigator.Commands { get; set; } = new CommandDictionary();

            event EventHandler<NavigateEventArgs> ILinkNavigator.PreviewNavigate {
                add { }
                remove { }
            }

            public void Navigate(Uri uri, FrameworkElement source, string parameter = null) {
                switch (uri.OriginalString) {
                    case "cmd://shareSettings?format=link":
                        ShareLinkCommand.Execute();
                        break;
                    case "cmd://shareSettings?format=markdown":
                        ShareMarkdownCommand.Execute();
                        break;
                    default:
                        BbCodeBlock.DefaultLinkNavigator.Navigate(uri, source, parameter);
                        break;
                }
            }

            public int CompareTo(object obj) {
                return obj is WebSource ws ? Name?.CompareTo(ws.Name ?? "") ?? 0 : 0;
            }
        }

        private static readonly Lazier<IReadOnlyCollection<string>> FoundDomains = Lazier.CreateAsync(async () => {
            using (var wc = KillerOrder.Create(new CookieAwareWebClient(), TimeSpan.FromSeconds(10)))
            using (wc.Victim.SetUserAgent(@"Seeker/1.0." + MathUtils.Random(1000, 9999))) {
                var data = await wc.Victim.DownloadStringTaskAsync(@"https://html.duckduckgo.com/html/?q=mods+assetto+corsa");
                return (IReadOnlyCollection<string>)Regex.Matches(data, @"result__a"" href=""([^""]+)""").OfType<Match>()
                        .Select(GetUrl).Where(SanityCheck).Take(18).ToList();
            }

            bool SanityCheck(string v) {
                var ret = !new[] {
                    0, 789142499, -413010326
                }.ArrayContains(-v?.GetHashCode() ?? 0);
#if DEBUG
                Logging.Debug("URL: " + v + ", hash: " + (-v?.GetHashCode() ?? 0) + ", passed: " + ret);
#endif
                return ret;
            }

            string GetUrl(Match x) {
                var p = x.Groups[1].Value.Split(new[] { @"uddg=" }, StringSplitOptions.RemoveEmptyEntries);
                var v = p.ArrayElementAtOrDefault(1)?.Split(new[] { @"&amp;" }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                return string.IsNullOrWhiteSpace(v) ? null
                        : Regex.Replace(Uri.UnescapeDataString(v), @"^https?://(?:www\.)?|/$", "", RegexOptions.IgnoreCase);
            }
        });

        public class ListViewModel : NotifyPropertyChanged, IComparer<WebSource>, ILoaderFactory {
            private static PluginsRequirement _requirement;
            public static PluginsRequirement Requirement => _requirement ?? (_requirement = new PluginsRequirement(KnownPlugins.CefSharp));

            public ChangeableObservableCollection<WebSource> WebSources { get; }
            public BetterListCollectionView WebSourcesView { get; }

            private readonly StoredValue<string> _selectedSourceId = Stored.Get<string>("ModsWebBrowser.SelectedSourceId");

            private WebSource _selectedSource;

            [CanBeNull]
            public WebSource SelectedSource {
                get => _selectedSource;
                set => Apply(value, ref _selectedSource, () => { _selectedSourceId.Value = value?.Id; });
            }

            private readonly Storage _storage;

            private void AddDefaultSources(IList<WebSource> source, bool addNewOnly) {
                void Add(string urlCheck, string urlValue) {
                    if (string.IsNullOrWhiteSpace(urlCheck) || string.IsNullOrWhiteSpace(urlValue)) return;

                    if (addNewOnly) {
                        var key = @".urlAdded:" + urlCheck;
                        if (_storage.Get(key, false)) return;
                        _storage.Set(key, true);
                    }

                    if (!source.Any(x => x.Url.Contains(urlCheck))) {
                        var newItem = WebSource.Deserialize(urlValue);
                        if (newItem != null) {
                            source.AddSorted(newItem);
                        }
                    }
                }

                try {
                    var data = FilesStorage.Instance.LoadContentFile(ContentCategory.Miscellaneous, "Websites.json");
                    if (data != null) {
                        foreach (var p in JsonConvert.DeserializeObject<JObject>(data)) {
                            var o = (JObject)p.Value;
                            if (!o.GetBoolValueOnly("originalOnly", false) && !AddRecommendedPorts.Value) continue;
                            if (!o.GetBoolValueOnly("paidOnly", false) && !AddRecommendedPaid.Value) continue;
                            if (!o.GetBoolValueOnly("driftingOnly", false) && !AddRecommendedDrifting.Value) continue;
                            Add(p.Key, o.GetStringValueOnly("data"));
                        }
                    } else {
                        Logging.Warning("File Website.json is missing");
                    }
                } catch (Exception e) {
                    Logging.Error(e);
                }

                if (source.Count == 0) {
                    Add(@"assettocorsamods.net",
                            @"fczBCsIwEIThe54i4D179yDUnPsMsiRrW0h3SnaL+vZWxKvH+Rj+2X07E7GZuKOgG6+ollScwvDVmD8cx8PD/Odv/mpiVNh2bvQUvaODGibchjymTadwKlAX9ZhWXjT/RsVDG7he9yOq8RLToir9DQ");
                }

                OnceAddedRecommended.Value = true;
            }

            public ListViewModel() {
                _storage = new Storage(FilesStorage.Instance.GetFilename("Websites.data"));
                WebSources = new ChangeableObservableCollection<WebSource>(_storage.Keys.Where(x => !x.StartsWith(@"."))
                        .Select(x => _storage.GetObject<WebSource>(x)).NonNull());
                AddDefaultSources(WebSources, true);
                WebSources.ItemPropertyChanged += OnItemPropertyChanged;
                WebSources.CollectionChanged += OnCollectionChanged;
                SelectedSource = WebSources.GetByIdOrDefault(_selectedSourceId.Value) ?? WebSources.FirstOrDefault();

                WebSourcesView = new BetterListCollectionView(WebSources);
                WebSourcesView.SortDescriptions.Add(new SortDescription(nameof(WebSource.Name), ListSortDirection.Ascending));

                FlexibleLoader.Register(this);
            }

            public StoredValue<bool> OnceAddedRecommended { get; } = Stored.Get("/ModsWebBrowser.OnceAddedRecommended", false);
            public StoredValue<bool> AddRecommendedPorts { get; } = Stored.Get("/ModsWebBrowser.AddRecommendedPorts", true);
            public StoredValue<bool> AddRecommendedPaid { get; } = Stored.Get("/ModsWebBrowser.AddRecommendedPaid", true);
            public StoredValue<bool> AddRecommendedDrifting { get; } = Stored.Get("/ModsWebBrowser.AddRecommendedDrifting", true);

            private DelegateCommand _addRecommendedSourcesCommand;

            public DelegateCommand AddRecommendedSourcesCommand
                => _addRecommendedSourcesCommand ?? (_addRecommendedSourcesCommand = new DelegateCommand(() => AddDefaultSources(WebSources, false)));

            private AsyncCommand _addNewSourceCommand;

            public AsyncCommand AddNewSourceCommand => _addNewSourceCommand ?? (_addNewSourceCommand = new AsyncCommand(async () => {
                var suggestions = new BetterObservableCollection<string>();
                FoundDomains.GetValueAsync().ContinueWithInMainThread(t => {
                    if (t.Result != null) {
                        suggestions.ReplaceEverythingBy_Direct(t.Result);
                    }
                }, TaskContinuationOptions.OnlyOnRanToCompletion).Ignore();

                var url = await Prompt.ShowAsync("Enter URL for a new website:", "Add new website", required: true, maxLength: 80, placeholder: "?",
                        suggestions: suggestions, comment: SuggestionsMessage());
                if (string.IsNullOrWhiteSpace(url)) return;

                var newSource = new WebSource(null, url.Trim());
                LastStep(newSource).Ignore();

                WebSources.AddSorted(newSource, this);
                SelectedSource = newSource;
            }));

            private static string SuggestionsMessage() {
                var source = $"[url={BbCodeBlock.EncodeAttribute(@"https://duckduckgo.com/html/?q=mods+assetto+corsa")}]DuckDuckGo[/url] search engine";
                return $"Suggestions, if any, are provided by {source}.\nContent Manager doesn’t encourage you to use any of them.";
            }

            private static async Task LastStep(WebSource source) {
                var extraDetailsLoaded = false;

                if (!SettingsHolder.WebBlocks.ModsAutoLoadRuleForNew || !SettingsHolder.WebBlocks.ModsAutoLoadExtraForNew) {
                    LoadExtraDetailsFromWebsite();
                }

                if (SettingsHolder.WebBlocks.ModsAutoLoadRuleForNew) {
                    var suggested = await GetSuggestionAsync(source.Url);
                    if (suggested != null) {
                        if (string.IsNullOrEmpty(source.AutoDownloadRule)) {
                            source.AutoDownloadRule = suggested.Rule;
                        }

                        if (SettingsHolder.WebBlocks.ModsAutoLoadExtraForNew) {
                            var s = WebSource.Deserialize(suggested.Data).FirstOrDefault();
                            if (s != null && s.AutoDownloadRule == suggested.Rule
                                    && string.Equals(CleanUp(s.Url), CleanUp(source.Url), StringComparison.OrdinalIgnoreCase)) {
                                source.Name = s.Name;
                                source.Favicon = s.Favicon;
                                extraDetailsLoaded = true;
                            }
                        }
                    }
                }

                LoadExtraDetailsFromWebsite();

                void LoadExtraDetailsFromWebsite() {
                    if (extraDetailsLoaded) return;
                    extraDetailsLoaded = true;
                    source.UpdateDisplayNameCommand.ExecuteAsync().Ignore();
                }

                string CleanUp(string url) {
                    return Regex.Replace(url, @"^(?:(?:https?)?://)?(?:www\.)?|(?<=\w)/?(?:[\?#].+)?$", "", RegexOptions.IgnoreCase);
                }
            }

            private readonly Busy _saveBusy = new Busy();

            private void Save() {
                _saveBusy.Yield(() => {
                    _storage.CleanUp(x => true);
                    foreach (var source in WebSources) {
                        _storage.SetObject(source.Id, source);
                    }
                });
            }

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

                foreach (WebSource source in WebSourcesView) {
                    if (source.IsEnabled && source.IsFavourite) {
                        linkGroup.Links.Add(new Link {
                            DisplayName = source.Name,
                            Icon = source.Favicon == null ? null : new BetterImage { Filename = source.Favicon },
                            Source = new Uri("/Pages/Miscellaneous/ModsWebBrowser.xaml", UriKind.Relative).AddQueryParam("Id", source.Id)
                        });
                    }
                }
            }

            private void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e) {
                switch (e.PropertyName) {
                    case nameof(WebSource.IsDeleted):
                        WebSources.Remove((WebSource)sender);
                        return;
                    case nameof(WebSource.IsEnabled):
                    case nameof(WebSource.IsFavourite):
                    case nameof(WebSource.Name):
                    case nameof(WebSource.Favicon):
                        RebuildLinks();
                        break;
                }

                Save();
            }

            private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e) {
                RebuildLinks();
                Save();
            }

            int IComparer<WebSource>.Compare(WebSource x, WebSource y) {
                return x?.Name?.CompareTo(y?.Name) ?? 0;
            }

            async Task<bool> ILoaderFactory.TestAsync(string url, CancellationToken cancellation) {
                if (GetSource(url, out _) != null) return true;

                if (SettingsHolder.WebBlocks.ModsAutoLoadRuleForUnknown) {
                    var suggested = await GetSuggestionAsync(url);
                    var source = suggested == null ? null : WebSource.Deserialize(suggested.Data).FirstOrDefault();
                    if (source == null || suggested.Rule != source.AutoDownloadRule
                            || !string.Equals(source.Url.GetDomainNameFromUrl(), url.GetDomainNameFromUrl(), StringComparison.OrdinalIgnoreCase)) {
                        return false;
                    }

                    Logging.Write("Suggested source is added: " + source.Name);
                    VirtualSources.Add(source);
                    return true;
                }

                return false;
            }

            Task<ILoader> ILoaderFactory.CreateAsync(string url, CancellationToken cancellation) {
                Logging.Debug(url);

                var source = GetSource(url, out var isVirtual);
                if (source != null) {
                    Logging.Debug("Fitting source: " + source.Name);
                    return Task.FromResult<ILoader>(new BrowserLoader(source, isVirtual, url));
                }

                Logging.Debug("No fitting sources found");

                if (SentToInstall.Contains(url)) {
                    Logging.Debug("Using fake source for installation command without details");
                    return Task.FromResult<ILoader>(new BrowserLoader(new WebSource(null, url) {
                        Name = url.GetDomainNameFromUrl(),
                        CaptureDownloads = true,
                        IsEnabled = true,
                    }, true, url));
                }

                return Task.FromResult<ILoader>(null);
            }
        }

        [CanBeNull]
        private static WebSource GetSource(string url, out bool isVirtual) {
            var domain = url.GetDomainNameFromUrl();

            var userSource = Instance.WebSources.Where(x => x.IsEnabled && x.CaptureDownloads).FirstOrDefault(
                    x => string.Equals(x.Url.GetDomainNameFromUrl(), domain, StringComparison.OrdinalIgnoreCase));
            if (userSource != null) {
                isVirtual = false;
                return userSource;
            }

            var virtualSource = VirtualSources.Where(x => x.IsEnabled && x.CaptureDownloads).FirstOrDefault(
                    x => string.Equals(x.Url.GetDomainNameFromUrl(), domain, StringComparison.OrdinalIgnoreCase));
            if (virtualSource != null) {
                isVirtual = true;
                return virtualSource;
            }

            isVirtual = true;
            return null;
        }

        private static readonly List<string> SentToInstall = new List<string>();
        private static readonly List<WebSource> VirtualSources = new List<WebSource>();

        private static readonly string StopKey = @"s" + StringExtension.RandomString(20);
        private static readonly string PauseKey = @"p" + StringExtension.RandomString(20);

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust"), ComVisible(true)]
        public class JsProxy : JsProxyCSharp {
            private JsBridge _bridge;

            public JsProxy(JsBridge bridge) : base(bridge) {
                _bridge = bridge;
            }

            [UsedImplicitly]
            public void DownloadFrom(string url) {
                if (string.IsNullOrWhiteSpace(url)) return;
                Sync(() => { _bridge.DownloadFromCallback?.Invoke(url); });
            }

            [UsedImplicitly]
            public void SetCssQuery(string value) {
                Sync(() => {
                    if (_bridge.Finder == null) return;
                    _bridge.Finder.Model.Value = value;
                });
            }
        }

        public class JsBridge : JsBridgeBase {
            internal Action<string> DownloadFromCallback;

            [CanBeNull]
            private ModsWebFinder _finder;

            [CanBeNull]
            internal ModsWebFinder Finder {
                get => _finder;
                set {
                    if (Equals(_finder, value)) return;
                    _finder = value;
                    if (value == null) {
                        StopCssSelector();
                    } else {
                        RunCssSelector();
                    }
                }
            }

            public override void PageLoaded(string url) {
                if (Finder == null) return;
                RunCssSelector();
            }

            protected override JsProxyBase MakeProxy() {
                return new JsProxy(this);
            }

            private void StopCssSelector() {
                Tab.Execute(@"window.$KEY && window.$KEY()".Replace(@"$KEY", StopKey));
            }

            private void RunCssSelector() {
                Tab.Execute(@"
if (window.$KEY) return;

var CssSelectorGenerator = (function(){var a,b,c=[].indexOf||function(a){for(var b=0,c=this.length;c>b;b++)if(b in this&&this[b]===a)return b;return-1};a=function(){function a(a){null==a&&(a={}),this.options={},this.setOptions(this.default_options),this.setOptions(a)}return a.prototype.default_options={selectors:['id','class','tag','nthchild']},a.prototype.setOptions=function(a){var b,c,d;null==a&&(a={}),c=[];for(b in a)d=a[b],this.default_options.hasOwnProperty(b)?c.push(this.options[b]=d):c.push(void 0);return c},a.prototype.isElement=function(a){return!(1!==(null!=a?a.nodeType:void 0))},a.prototype.getParents=function(a){var b,c;if(c=[],this.isElement(a))for(b=a;this.isElement(b);)c.push(b),b=b.parentNode;return c},a.prototype.getTagSelector=function(a){return this.sanitizeItem(a.tagName.toLowerCase())},a.prototype.sanitizeItem=function(a){var b;return b=a.split('').map(function(a){return':'===a?'\\'+':'.charCodeAt(0).toString(16).toUpperCase()+' ':/[ !""'#$%&'()*+,.\/;<=>?@\[\\\]^`{|}~]/.test(a)?'\\'+a:escape(a).replace(/\%/g,'\\')}),b.join('')},a.prototype.getIdSelector=function(a){var b,c;return b=a.getAttribute('id'),null==b||''===b||/\s/.exec(b)||/^\d/.exec(b)||(c='#'+this.sanitizeItem(b),1!==a.ownerDocument.querySelectorAll(c).length)?null:c},a.prototype.getClassSelectors=function(a){var b,c,d;return d=[],b=a.getAttribute('class'),null!=b&&(b=b.replace(/\s+/g,' '),b=b.replace(/^\s|\s$/g,''),''!==b&&(d=function(){var a,d,e,f;for(e=b.split(/\s+/),f=[],a=0,d=e.length;d>a;a++)c=e[a],f.push('.'+this.sanitizeItem(c));return f}.call(this))),d},a.prototype.getAttributeSelectors=function(a){var b,d,e,f,g,h,i;for(i=[],d=['id','class'],g=a.attributes,e=0,f=g.length;f>e;e++)b=g[e],h=b.nodeName,c.call(d,h)<0&&i.push('['+b.nodeName+'='+b.nodeValue+']');return i},a.prototype.getNthChildSelector=function(a){var b,c,d,e,f,g;if(e=a.parentNode,null!=e)for(b=0,g=e.childNodes,c=0,d=g.length;d>c;c++)if(f=g[c],this.isElement(f)&&(b++,f===a))return b==1?':first-child':':nth-child('+b+')';return null},a.prototype.testSelector=function(a,b){var c,d;return c=!1,null!=b&&''!==b&&(d=a.ownerDocument.querySelectorAll(b),1===d.length&&d[0]===a&&(c=!0)),c},a.prototype.getAllSelectors=function(a){var b;return b={t:null,i:null,c:null,a:null,n:null},c.call(this.options.selectors,'tag')>=0&&(b.t=this.getTagSelector(a)),c.call(this.options.selectors,'id')>=0&&(b.i=this.getIdSelector(a)),c.call(this.options.selectors,'class')>=0&&(b.c=this.getClassSelectors(a)),c.call(this.options.selectors,'attribute')>=0&&(b.a=this.getAttributeSelectors(a)),c.call(this.options.selectors,'nthchild')>=0&&(b.n=this.getNthChildSelector(a)),b},a.prototype.testUniqueness=function(a,b){var c,d;return d=a.parentNode,c=d.querySelectorAll(b),1===c.length&&c[0]===a},a.prototype.testCombinations=function(a,b,c){var d,e,f,g,h,i,j;for(i=this.getCombinations(b),e=0,g=i.length;g>e;e++)if(d=i[e],this.testUniqueness(a,d))return d;if(null!=c)for(j=b.map(function(a){return c+a}),f=0,h=j.length;h>f;f++)if(d=j[f],this.testUniqueness(a,d))return d;return null},a.prototype.getUniqueSelector=function(a){var b,c,d,e,f,g;for(g=this.getAllSelectors(a),e=this.options.selectors,c=0,d=e.length;d>c;c++)switch(f=e[c]){case'id':if(null!=g.i)return g.i;break;case'class':if(null!=g.c&&0!==g.c.length&&(b=this.testCombinations(a,g.c,g.t)))return b;break;case'tag':if(null!=g.t&&this.testUniqueness(a,g.t))return g.t;break;case'attribute':if(null!=g.a&&0!==g.a.length&&(b=this.testCombinations(a,g.a,g.t)))return b;break;case'nthchild':if(null!=g.n)return g.n}return'*'},a.prototype.getSelector=function(a){var b,c,d,e,f,g,h,i,j,k;for(b=[],h=this.getParents(a),d=0,f=h.length;f>d;d++)c=h[d],j=this.getUniqueSelector(c),null!=j&&b.push(j);for(k=[],e=0,g=b.length;g>e&&b[e]!='body';e++){k.unshift(c=b[e]);if(c[0]=='#')break}return k.join(' > ')},a.prototype.getCombinations=function(a){var b,c,d,e,f,g,h;for(null==a&&(a=[]),h=[[]],b=d=0,f=a.length-1;f>=0?f>=d:d>=f;b=f>=0?++d:--d)for(c=e=0,g=h.length-1;g>=0?g>=e:e>=g;c=g>=0?++e:--e)h.push(h[c].concat(a[b]));return h.shift(),h=h.sort(function(a,b){return a.length-b.length}),h=h.map(function(a){return a.join('')})},a}();return a})();

var DomOutline = function(e){e=e||{};var t={},n='$NAMESPACE',o={keyCodes:{BACKSPACE:8,ESC:27,DELETE:46},active:!1,initialized:!1,elements:{}};function d(){var e,t;!0!==o.initialized&&(e='.'+n+'{background:$ACCENT;position:absolute;z-index:1000000;}.'+n+'_label{background:$ACCENT;border-radius:2px;color:#fff;font:bold 12px/12px Helvetica, sans-serif;padding:4px 6px;position:absolute;text-shadow:0 1px 1px rgba(0, 0, 0, 0.25);z-index: 1000001;}',(t=document.getElementsByTagName('head')[0].appendChild(document.createElement('style'))).type='text/css',t.styleSheet?t.styleSheet.cssText=e:t.innerHTML=e,o.initialized=!0)}function l(e,t,n,o,d){e.style.left=t+'px',e.style.top=n+'px',e.style.width=o+'px',e.style.height=d+'px'}function i(e){if(-1===e.target.className.indexOf(n)){t.element=e.target;var d,i,a,s,m=void 0!==window.pageYOffset?window.pageYOffset:(document.documentElement||document.body.parentNode||document.body).scrollTop,c=t.element.getBoundingClientRect(),r=c.top+m;o.elements.label.style.top=Math.max(0,r-20-2,m)+'px',o.elements.label.style.left=Math.max(0,c.left-2)+'px',o.elements.label.textContent=(d=t.element,i=c.width,a=c.height,s=d.tagName.toLowerCase(),d.id&&(s+='#'+d.id),d.className&&(s+=('.'+d.className.trim().replace(/ /g,'.')).replace(/\.\.+/g,'.')),s+' ('+Math.round(i)+'×'+Math.round(a)+')'),l(o.elements.top,c.left-2,r-2,c.width+2+2,2),l(o.elements.bottom,c.left-2,r+c.height,c.width+2+2,2),l(o.elements.left,c.left-2,r,2,c.height),l(o.elements.right,c.left+c.width,r,2,c.height)}}function a(e){e.keyCode!==o.keyCodes.ESC&&e.keyCode!==o.keyCodes.BACKSPACE&&e.keyCode!==o.keyCodes.DELETE||t.stop()}function s(n){e.onClick.call(t.element,n)}return t.start=function(){d(),!0!==o.active&&(o.active=!0,function(){var e=document.createElement('div');e.classList.add(n+'_label');var t=document.createElement('div'),d=document.createElement('div'),l=document.createElement('div'),i=document.createElement('div');t.classList.add(n),d.classList.add(n),l.classList.add(n),i.classList.add(n);var a=document.body;o.elements.label=a.appendChild(e),o.elements.top=a.appendChild(t),o.elements.bottom=a.appendChild(d),o.elements.left=a.appendChild(l),o.elements.right=a.appendChild(i)}(),document.body.addEventListener('mousemove',i),document.body.addEventListener('keyup',a),setTimeout(function(){document.body.addEventListener('click',s)},50))},t.stop=function(){o.active=!1,function(){for(var e in o.elements){var t=o.elements[e];t.parentNode.removeChild(t)}}(),document.body.removeEventListener('mousemove',i),document.body.removeEventListener('keyup',a),document.body.removeEventListener('click',s)},t};

var outline = DomOutline({ onClick: function (e){
    if (window.$PAUSEKEY) return;
    e.preventDefault(); e.stopPropagation();
    window.external.SetCssQuery(new CssSelectorGenerator({ selectors: [ 'id', 'class', 'tag', 'nthchild' ] }).getSelector(this));
} });
outline.start();
window.$KEY = outline.stop.bind(outline);

".Replace(@"$ACCENT", AppAppearanceManager.Instance.AccentColor.ToHexString())
                        .Replace(@"$NAMESPACE", @"__" + StringExtension.RandomString(20))
                        .Replace(@"$KEY", StopKey)
                        .Replace(@"$PAUSEKEY", PauseKey), true);
            }
        }

        private class BrowserLoader : ILoader, IWebDownloadListener, ILinkNavigator {
            [NotNull]
            private readonly WebSource _source;

            private bool _isVirtual;

            private readonly string _url;

            public ILoader Parent { get; set; }

            public BrowserLoader([NotNull] WebSource source, bool isVirtual, string url) {
                _source = source;
                _isVirtual = isVirtual;
                _url = url;
            }

            private void MarkParent(BrowserLoader parent) {
                if (parent._source.Id == _source.Id) return;
                parent._source.AddRedirectsTo(_source.Id);
            }

            public long? TotalSize { get; private set; }
            public string FileName { get; private set; }
            public string Version => null;

            public bool UsesClientToDownload => false;
            public bool CanPause => false;

            private WebBlock _webBlock;

            Task<bool> ILoader.PrepareAsync(CookieAwareWebClient client, CancellationToken cancellation) {
                for (var parent = Parent; parent != null; parent = parent.Parent) {
                    if (parent is BrowserLoader browserLoader) {
                        MarkParent(browserLoader);
                    }
                }

                return Task.FromResult(true);
            }

            private async void LoadViaLoader(string url) {
                var webClient = _webClient;
                var destinationCallback = _destinationCallback;
                var reportDestinationCallback = _reportDestinationCallback;
                var progress = _progress;
                var resultTask = _resultTask;

                if (destinationCallback == null) return;

                var firstDownload = !_onDownloadFired;
                _onDownloadFired = true;
                _destinationCallback = null;
                _reportDestinationCallback = null;
                _progress = null;

                try {
                    var newLoader = await FlexibleLoader.CreateLoaderAsync(url, this, default(CancellationToken));
                    if (newLoader == null) throw new Exception("Unexpected exception #4313");

                    Logging.Write("Loader: " + newLoader.GetType().Name);

                    if (!await newLoader.PrepareAsync(webClient, _cancellation)) {
                        throw new InformativeException("Can’t load file", "Loader preparation failed.");
                    }

                    TotalSize = newLoader.TotalSize;
                    FileName = newLoader.FileName;
                    _dialog.Hide();

                    if (firstDownload && SettingsHolder.WebBlocks.NotifyOnWebDownloads) {
                        Toast.Show("New download started", FileName ?? url);
                    }

                    var result = await newLoader.DownloadAsync(webClient, destinationCallback, reportDestinationCallback, null, progress, _cancellation);
                    resultTask.TrySetResult(result);
                } catch (Exception e) {
                    resultTask.TrySetException(e);
                }
            }

            private CookieAwareWebClient _webClient;
            private FlexibleLoaderGetPreferredDestinationCallback _destinationCallback;
            private FlexibleLoaderReportDestinationCallback _reportDestinationCallback;
            private IProgress<long> _progress;
            private CancellationToken _cancellation;
            private ModernDialog _dialog;
            private BbCodeBlock _message;
            private ModsWebFinder _finder;
            private TaskCompletionSource<string> _resultTask;
            private CancellationTokenSource _testToken;
            private string _testMessage;
            private bool _onDownloadFired;

            void IWebDownloadListener.OnDownload(string url, string suggestedName, long totalSize, IWebDownloader downloader) {
                var firstDownload = !_onDownloadFired;
                _onDownloadFired = true;
                ActionExtension.InvokeInMainThread(async () => {
                    Logging.Write("URL to download: " + url);

                    if (_testToken != null) {
                        Logging.Debug("Test mode");
                        _testMessage = $" • Name: {suggestedName};\n"
                                + (totalSize >= 0 ? $" • Size: {totalSize.ToReadableSize()};\n" : "")
                                + $" • URL: {url}.";
                        _testToken.Cancel();
                        return;
                    }

                    var destinationCallback = _destinationCallback;
                    var reportDestinationCallback = _reportDestinationCallback;
                    var progress = _progress;
                    var resultTask = _resultTask;

                    if (destinationCallback == null) return;

                    TotalSize = totalSize;
                    FileName = suggestedName;

                    _destinationCallback = null;
                    _reportDestinationCallback = null;
                    _progress = null;

                    var destination = destinationCallback(url, new FlexibleLoaderMetaInformation {
                        CanPause = false,
                        FileName = suggestedName,
                        TotalSize = totalSize
                    });

                    reportDestinationCallback?.Invoke(destination.Filename);
                    _dialog.Hide();

                    try {
                        if (firstDownload && SettingsHolder.WebBlocks.NotifyOnWebDownloads) {
                            Toast.Show("New download started", suggestedName ?? url);
                        }

                        resultTask.TrySetResult(await downloader.DownloadAsync(destination.Filename, progress, _cancellation));
                    } catch (Exception e) {
                        Logging.Warning(e);
                        resultTask.TrySetException(e);
                    }
                });
            }

            private string GetMessage() {
                return _isVirtual
                        ? "Find a download button and click it to install. This website is not added to the list, [url=\"cmd://addWebsite\"]click here[/url] to add it."
                        : PluginsManager.Instance.IsPluginEnabled(KnownPlugins.CefSharp)
                                ? "Find a download button and click it to install, or [url=\"cmd://setRule\"]describe to CM where download button is[/url] so it could click it automatically."
                                : "Find a download button and click it to install. Enable CefSharp plugin to get an access to extra options.";
            }

            private string GetFailedMessage() {
                return _isVirtual
                        ? "App failed to start download, please, click the button manually. This website is not added to the list, [url=\"cmd://addWebsite\"]click here[/url] to add it."
                        : "App failed to start download. Please, click the button manually, or [url=\"cmd://setRule\"]change the describtion of where download button is[/url]. Also, please, make sure the download button doesn’t redirect to a unknown website.";
            }

            async Task<string> ILoader.DownloadAsync(CookieAwareWebClient client, FlexibleLoaderGetPreferredDestinationCallback getPreferredDestination,
                    FlexibleLoaderReportDestinationCallback reportDestination, Func<bool> checkIfPaused, IProgress<long> progress,
                    CancellationToken cancellation) {
                await Task.Delay(500);

                _webClient = client;
                _destinationCallback = getPreferredDestination;
                _reportDestinationCallback = reportDestination;
                _progress = progress;
                _cancellation = cancellation;

                _webBlock = new WebBlock {
                    StartPage = _url,
                    NewWindowsBehavior = NewWindowsBehavior.Callback,
                    UserAgent = _source.UserAgent,
                    IsAddressBarVisible = true,
                    DownloadListener = this,
                    Margin = new Thickness(0, 0, 0, 12),
                };
                _webBlock.PageLoaded += (sender, args) => Logging.Warning(args.Tab.LoadedUrl);
                _webBlock.NewWindow += OnNewWindow;

                _finder = new ModsWebFinder(_source) {
                    Margin = new Thickness(24, 0, 24, 0),
                    Visibility = Visibility.Collapsed
                };

                DockPanel.SetDock(_finder, Dock.Bottom);
                _webBlock.SetJsBridge<JsBridge>(x => {
                    x.DownloadFromCallback = OnDownloadFromCallback;
                    x.Finder = null;
                });

                _message = new BbCodeBlock {
                    Text = GetMessage(),
                    MaxWidth = 480,
                    VerticalAlignment = VerticalAlignment.Center,
                    LinkNavigator = this
                };
                _message.SetResourceReference(TextBlock.FontSizeProperty, @"SmallFontSize");

                _dialog = new ModernDialog {
                    Content = new DockPanel { Children = { _finder, _webBlock } },
                    Width = 640,
                    Height = 720,
                    MaxWidth = DpiAwareWindow.UnlimitedSize,
                    MaxHeight = DpiAwareWindow.UnlimitedSize,
                    SizeToContent = SizeToContent.Manual,
                    ResizeMode = ResizeMode.CanResizeWithGrip,
                    LocationAndSizeKey = "DownloadViaWebBrowser",
                    Padding = new Thickness(0, 20, 0, 8),
                    Title = $"Download from {_source.Name}",
                    ShowInTaskbar = true,
                    Owner = null,
                    ButtonsRowContent = _message,
                    ButtonsRowContentAlignment = HorizontalAlignment.Left,
                };
                _dialog.Buttons = new[] { FixButton(_dialog.CancelButton) };

                var finished = false;
                _resultTask = new TaskCompletionSource<string>();
                _dialog.Closed += (sender, args) => Cancel();

                if (_source.AutoDownloadRule != null && PluginsManager.Instance.IsPluginEnabled(KnownPlugins.CefSharp)) {
                    AutoDownload();
                } else {
                    _dialog.Show();
                    _dialog.BringToFront();
                }

                cancellation.Register(Cancel);
                _resultTask.Task.ContinueWith(t => ActionExtension.InvokeInMainThreadAsync(() => _dialog.Close()), cancellation).Ignore();
                return await _resultTask.Task;

                void AutoDownload() {
                    string suggestedRule = null;
                    _webBlock.PageLoaded += async (sender, args) => {
                        var targetUrl = _url;
                        if (targetUrl == null) return;

                        var url = args.Tab.LoadedUrl;
                        Logging.Debug("URL: " + url);
                        if (string.Equals(url.GetDomainNameFromUrl(), targetUrl.GetDomainNameFromUrl(), StringComparison.OrdinalIgnoreCase)) {
                            Logging.Debug(url);
                            GetSuggestionAsync(targetUrl).ContinueWith(r => {
                                Logging.Debug("Suggested rule: " + r.Result?.Rule);
                                suggestedRule = r.Result?.Rule;
                            }).Ignore();
                            Logging.Debug("Run rule: " + _source.AutoDownloadRule);
                            RunRule(_source.AutoDownloadRule);
                        } else if (await FlexibleLoader.IsSupportedAsync(url, default(CancellationToken))) {
                            // Create a Loader and use it
                            Logging.Write("There is a loader for URL: " + url);

                            if (_testToken != null) {
                                _testMessage = $" • Redirect to known website: {url}.";
                                _testToken.Cancel();
                                return;
                            }

                            LoadViaLoader(url);
                        } else {
                            // Not supported and unknown, skip
                            Logging.Write("No loader for URL: " + url);
                        }
                    };

                    Task.Delay(TimeSpan.FromSeconds(7d)).ContinueWith(t => {
                        if (suggestedRule != null && suggestedRule != _source.AutoDownloadRule) {
                            RunRule(suggestedRule);
                            Task.Delay(TimeSpan.FromSeconds(7d)).ContinueWith(t1 => {
                                if (_onDownloadFired) {
                                    _source.AutoDownloadRule = suggestedRule;
                                } else {
                                    ActionExtension.InvokeInMainThread(FailSafe);
                                }
                            });
                            return;
                        }

                        if (!_onDownloadFired) {
                            ActionExtension.InvokeInMainThread(FailSafe);
                        }
                    }).Ignore();
                    _dialog.ShowInvisible();
                }

                async void FailSafe() {
                    if (finished) return;
                    _ranRule = false;
                    _dialog.Show();
                    _message.Text = GetFailedMessage();

                    await Task.Delay(500);
                    _dialog.BringToFront();
                }

                void Cancel() {
                    finished = true;
                    _resultTask.TrySetCanceled();
                    if (!_dialog.IsClosed()) {
                        ActionExtension.InvokeInMainThreadAsync(() => _dialog.Close());
                    }
                }
            }

            private async void OnDownloadFromCallback(string url) {
                try {
                    if (_testToken == null && !_ranRule) {
                        // Not in auto-download mode, usual behavior
                        _webBlock.OpenNewTab(url);
                        return;
                    }

                    Logging.Write("Download from: " + url);
                    if (string.Equals(url.GetDomainNameFromUrl(), _url.GetDomainNameFromUrl(), StringComparison.OrdinalIgnoreCase)) {
                        _webBlock.OpenNewTab(url);
                        return;
                    }

                    if (await FlexibleLoader.IsSupportedAsync(url, default(CancellationToken))) {
                        // Create a Loader and use it
                        Logging.Write("There is a loader for URL: " + url);

                        if (_testToken != null) {
                            _testMessage = $" • Redirect to known website: {url}.";
                            _testToken.Cancel();
                            return;
                        }

                        LoadViaLoader(url);
                    } else {
                        // Not supported and unknown, skip
                        Logging.Write("No loader for URL: " + url);
                    }
                } catch (Exception e) {
                    Logging.Error(e);
                }
            }

            private async void OnNewWindow(object o, NewWindowEventArgs args) {
                args.Cancel = true;

                if (_testToken == null && !_ranRule) {
                    // Not in auto-download mode, usual behavior
                    OpenNewTab();
                    return;
                }

                Logging.Write("New window: " + args.Url);
                if (args.Url.GetDomainNameFromUrl() == _url.GetDomainNameFromUrl()) {
                    OpenNewTab();
                    return;
                }

                void OpenNewTab() {
                    _webBlock.OpenNewTab(args.Url);
                }

                if (await FlexibleLoader.IsSupportedAsync(args.Url, default(CancellationToken))) {
                    // Create a Loader and use it
                    Logging.Write("There is a loader for URL: " + args.Url);

                    if (_testToken != null) {
                        _testMessage = $" • Redirect to known website: {args.Url}.";
                        _testToken.Cancel();
                        return;
                    }

                    LoadViaLoader(args.Url);
                } else {
                    // Not supported and unknown, skip
                    Logging.Write("No loader for URL: " + args.Url);
                }
            }

            public Task<string> GetDownloadLink(CancellationToken cancellation) {
                return Task.FromResult(_url);
            }

            CommandDictionary ILinkNavigator.Commands { get; set; } = new CommandDictionary();

            event EventHandler<NavigateEventArgs> ILinkNavigator.PreviewNavigate {
                add { }
                remove { }
            }

            private bool _ranRule;

            private void RunRule(string rule) {
                rule = rule.Trim();
                _ranRule = true;
                _webBlock.CurrentTab?.Execute(WebSource.GetActionScript(rule));
            }

            private AsyncCommand<CancellationToken?> _testRuleCommand;

            public AsyncCommand<CancellationToken?> TestRuleCommand => _testRuleCommand ?? (_testRuleCommand = new AsyncCommand<CancellationToken?>(async c => {
                try {
                    bool result;
                    using (var token = new CancellationTokenSource())
                    using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token.Token, c ?? default(CancellationToken))) {
                        try {
                            _testToken = token;
                            _testMessage = null;
                            RunRule(_finder.Model.Value);
                            await Task.Delay(5000, linked.Token);
                        } finally {
                            if (ReferenceEquals(_testToken, token)) {
                                _testToken = null;
                                _ranRule = false;
                            }
                        }

                        result = token.IsCancellationRequested;
                    }

                    DisplayResult(result);
                } catch (Exception e) when (e.IsCancelled()) {
                    DisplayResult(true);
                }

                void DisplayResult(bool triggered) {
                    if (c?.IsCancellationRequested == true) return;
                    ModernDialog.ShowMessage(triggered && _testMessage != null
                            ? "Download triggered:\n" + _testMessage
                            : "Download wasn’t triggered. Please, make sure the download button doesn’t redirect to a unknown website.",
                            AppStrings.Common_Test, MessageBoxButton.OK);
                }
            }, c => !string.IsNullOrWhiteSpace(_finder.Model.Value)).ListenOn(_finder.Model, nameof(_finder.Model.Value)));

            private AsyncCommand _saveRuleCommand;

            public AsyncCommand SaveRuleCommand => _saveRuleCommand ?? (_saveRuleCommand = new AsyncCommand(Save,
                    () => !string.IsNullOrWhiteSpace(_finder.Model.Value)).ListenOn(_finder.Model, nameof(_finder.Model.Value)));

            private async Task Save() {
                _source.AutoDownloadRule = _finder.Model.Value;
                _message.Text = "Please, wait for newly created rule to start download…";
                CancelRuleEditing();

                using (var token = new CancellationTokenSource()) {
                    try {
                        _onDownloadFired = false;
                        RunRule(_source.AutoDownloadRule);
                        await Task.Delay(5000, token.Token);
                    } catch (OperationCanceledException) { }
                }

                if (!_onDownloadFired) {
                    _message.Text = GetFailedMessage();
                }
            }

            private static Button FixButton(Button btn) {
                btn.VerticalAlignment = VerticalAlignment.Bottom;
                btn.Height = 21d;
                return btn;
            }

            private void CancelRuleEditing() {
                _dialog.Buttons = new[] { FixButton(_dialog.CancelButton) };
                _message.Visibility = Visibility.Visible;
                _finder.Visibility = Visibility.Collapsed;
                _webBlock.SetJsBridge<JsBridge>(x => x.Finder = null);

                var token = _testToken;
                if (token != null) {
                    _testToken = null;
                    token.Cancel();
                }
            }

            private void SetRule() {
                _dialog.Buttons = new[] {
                    FixButton(ModernDialog.CreateExtraDialogButton<AsyncButton>("Test rule", TestRuleCommand)),
                    FixButton(ModernDialog.CreateExtraDialogButton("Save rule", SaveRuleCommand)),
                    FixButton(ModernDialog.CreateExtraDialogButton("Cancel rule editing", CancelRuleEditing)),
                };

                _message.Visibility = Visibility.Hidden;
                _finder.Visibility = Visibility.Visible;
                _webBlock.SetJsBridge<JsBridge>(x => x.Finder = _finder);
            }

            void ILinkNavigator.Navigate(Uri uri, FrameworkElement source, string parameter) {
                switch (uri.OriginalString) {
                    case "cmd://setRule":
                        SetRule();
                        break;
                    case "cmd://addWebsite":
                        if (_isVirtual) {
                            Instance.WebSources.Add(_source);
                            _isVirtual = false;
                            _message.Text = GetMessage();
                        }

                        break;
                }
            }
        }

        public class ViewModel : NotifyPropertyChanged {
            [NotNull]
            public WebSource Source { get; }

            internal int DownloadPageCheckId;

            private string _downloadPageUrl;

            public string DownloadPageUrl {
                get => _downloadPageUrl;
                set => Apply(value, ref _downloadPageUrl, () => {
                    _installCommand?.RaiseCanExecuteChanged();
                    _shareLinkCommand?.RaiseCanExecuteChanged();
                });
            }

            private DelegateCommand _installCommand;

            public DelegateCommand InstallCommand => _installCommand ?? (_installCommand = new DelegateCommand(
                    () => ContentInstallationManager.Instance.InstallAsync(DownloadPageUrl, ContentInstallationParams.DefaultWithExecutables),
                    () => DownloadPageUrl != null));

            private DelegateCommand _shareLinkCommand;

            public DelegateCommand ShareLinkCommand => _shareLinkCommand ?? (_shareLinkCommand = new DelegateCommand(() => {
                var link = $@"{InternalUtils.MainApiDomain}/s/q:install?url={Uri.EscapeDataString(DownloadPageUrl)}&fromWebsite=1";
                foreach (var source in Source.WithChildren(true).Where(x => WebSource.IsRuleSafe(x.AutoDownloadRule))) {
                    var p = source.Serialize(WebSourceSerializationMode.Compact, DownloadPageUrl);
                    if (p != null && link.Length + p.Length < 1600) {
                        link += $@"&websiteData={Uri.EscapeDataString(p)}";
                    }
                }

                SharingUiHelper.ShowShared($"Link to install from {Source.Name}", link, true);
            }, () => DownloadPageUrl != null));

            public ViewModel([NotNull] WebSource source) {
                Source = source;
            }
        }

        public static async Task<int?> ImportWebsitesAsync([NotNull] string[] websiteData,
                [CanBeNull] Func<IEnumerable<string>, Task<bool>> unsafeWarningCallback) {
            var items = WebSource.Deserialize(websiteData).Where(x => GetSource(x.Url, out _) == null).ToList();
            if (items.Any(x => !WebSource.IsRuleSafe(x.AutoDownloadRule))) {
                if (unsafeWarningCallback != null && !await unsafeWarningCallback.Invoke(items.Select(x => x.Name))) {
                    return null;
                }
            }

            foreach (var item in items) {
                Instance.WebSources.Add(item);
            }

            return items.Count;
        }

        public static void PrepareForCommand(string[] urls, [NotNull] string[] websiteData) {
            foreach (var source in WebSource.Deserialize(websiteData, urls).Where(x => GetSource(x.Url, out _) == null)) {
                VirtualSources.Add(source);
            }

            SentToInstall.AddRange(urls.Where(x => GetSource(x, out _) == null));
        }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust"), ComVisible(true)]
        public class CheckingJsProxy : JsProxyCSharp {
            private CheckingJsBridge _bridge;

            public CheckingJsProxy(CheckingJsBridge bridge) : base(bridge) {
                _bridge = bridge;
            }

            [UsedImplicitly]
            public void Callback(bool elementFound) {
                _bridge.CallbackFn?.Invoke(elementFound);
            }
        }

        public class CheckingJsBridge : JsBridgeBase {
            internal Action<bool> CallbackFn;

            protected override JsProxyBase MakeProxy() {
                return new CheckingJsProxy(this);
            }
        }

        private async void CheckWebPage(WebBlock web) {
            var m = _model;
            if (m == null) return;

            var tab = web.CurrentTab;
            var url = tab?.LoadedUrl;
            if (url == null) {
                m.DownloadPageUrl = null;
                return;
            }

            if (FlexibleLoader.IsSupportedFileStorage(url)) {
                m.DownloadPageUrl = url;
                return;
            }

            if (!m.Source.CaptureDownloads) return;

            var rule = m.Source.AutoDownloadRule?.Trim();
            if (string.IsNullOrEmpty(rule)) {
                m.DownloadPageUrl = null;
                return;
            }

            var checkId = ++m.DownloadPageCheckId;
            var ctsRef = new CancellationTokenSource[] { null };
            bool? resultRef = null;

            void ObjCallbackFn(bool v) {
                resultRef = v;
                ctsRef[0]?.Cancel();
            }

            using (var cts = new CancellationTokenSource()) {
                ctsRef[0] = cts;
                web.SetJsBridge<CheckingJsBridge>(x => x.CallbackFn = ObjCallbackFn);
                tab.Execute(WebSource.GetCheckScript(rule));

                try {
                    await Task.Delay(1000, cts.Token);
                } catch (OperationCanceledException) { }

                if (m.DownloadPageCheckId != checkId) return;
                m.DownloadPageUrl = resultRef == true ? url : null;
                ctsRef[0] = null;
            }
        }

        private void OnCurrentTabChanged(object sender, EventArgs e) {
            CheckWebPage((WebBlock)sender);
        }

        private void OnPageLoading(object sender, WebTabEventArgs e) {
            if (_model != null) {
                _model.DownloadPageUrl = null;
            }
        }

        private void OnPageLoaded(object sender, WebTabEventArgs e) {
            CheckWebPage((WebBlock)sender);
        }
    }
}