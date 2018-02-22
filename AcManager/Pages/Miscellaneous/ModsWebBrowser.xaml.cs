using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using AcManager.Controls;
using AcManager.Controls.Presentation;
using AcManager.Controls.UserControls;
using AcManager.Controls.UserControls.Web;
using AcManager.Pages.Windows;
using AcManager.Tools;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Helpers.Loaders;
using AcManager.UserControls;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows;
using FirstFloor.ModernUI.Windows.Controls;
using FirstFloor.ModernUI.Windows.Navigation;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Pages.Miscellaneous {
    public partial class ModsWebBrowser : IParametrizedUriContent {
        public static ListViewModel Instance { get; private set; }

        public static void Initialize() {
            Instance = new ListViewModel();
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

            [JsonProperty("downloadBtnSelector")]
            private string _downloadButtonSelector;

            public string DownloadButtonSelector {
                get => _downloadButtonSelector;
                set => Apply(value, ref _downloadButtonSelector, () => Logging.Debug(value));
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
                var data = await wc.Victim.DownloadStringTaskAsync(@"https://duckduckgo.com/html/?q=assetto+corsa+mods");
                return (IReadOnlyCollection<string>)Regex.Matches(data, @"result__a"" href=""([^""]+)""").OfType<Match>()
                                                         .Select(GetUrl).Where(SanityCheck).Take(10).ToList();
            }

            bool SanityCheck(string v) {
                return !new[] { 0, 1491093284, 518443847, 1564670876, 110165427 }.ArrayContains(-v?.GetHashCode() ?? 0);
            }

            string GetUrl(Match x) {
                var p = x.Groups[1].Value.Split(new[] { @"uddg=" }, StringSplitOptions.RemoveEmptyEntries);
                var v = p.ArrayElementAtOrDefault(1)?.Split(new[] { @"&amp;" }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                return string.IsNullOrWhiteSpace(v) ? null
                        : Regex.Replace(Uri.UnescapeDataString(v), @"^https?://(?:www\.)?|/$", "", RegexOptions.IgnoreCase);
            }
        });

        public class ListViewModel : NotifyPropertyChanged, IComparer<WebSource>, ILoaderFactory {
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

                FlexibleLoader.Register(this);
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

            int IComparer<WebSource>.Compare(WebSource x, WebSource y) {
                return x?.Name?.CompareTo(y?.Name) ?? 0;
            }

            ILoader ILoaderFactory.Create(string url) {
                Logging.Debug(url);

                var source = WebSources.Where(x => x.IsEnabled && x.CaptureDownloads).FirstOrDefault(
                        x => string.Equals(x.Url.GetDomainNameFromUrl(), url.GetDomainNameFromUrl(), StringComparison.OrdinalIgnoreCase));
                if (source == null) {
                    Logging.Debug("No fitting sources found");
                    return null;
                }

                Logging.Debug("Fitting source: " + source.Name);
                return new BrowserLoader(source, url);
            }
        }

        private class BrowserLoader : ILoader, IWebDownloadListener, ILinkNavigator {
            private readonly WebSource _source;
            private readonly string _url;

            public BrowserLoader(WebSource source, string url) {
                _source = source;
                _url = url;
            }

            public long? TotalSize { get; private set; }
            public string FileName { get; private set; }
            public string Version => null;

            public bool UsesClientToDownload => false;
            public bool CanPause => false;

            private WebBlock _webBlock;

            Task<bool> ILoader.PrepareAsync(CookieAwareWebClient client, CancellationToken cancellation) {
                return Task.FromResult(true);
            }

            private FlexibleLoaderGetPreferredDestinationCallback _destinationCallback;
            private FlexibleLoaderReportDestinationCallback _reportDestinationCallback;
            private IProgress<long> _progress;
            private CancellationToken _cancellation;
            private ModernDialog _dialog;
            private BbCodeBlock _message;
            private ModsWebFinder _finder;
            private TaskCompletionSource<string> _resultTask;

            void IWebDownloadListener.OnDownload(string url, string suggestedName, long totalSize, IWebDownloader downloader) {
                ActionExtension.InvokeInMainThread(async () => {
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
                        resultTask.TrySetResult(await downloader.Download(destination.Filename, progress, _cancellation));
                    } catch (Exception e) {
                        Logging.Warning(e);
                        resultTask.TrySetException(e);
                    }
                });
            }

            [PermissionSet(SecurityAction.Demand, Name = "FullTrust"), ComVisible(true)]
            private class JsBridge : JsBridgeBase {
                [CanBeNull]
                private ModsWebFinder _finder;

                [CanBeNull]
                public ModsWebFinder Finder {
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

                [UsedImplicitly]
                public void SetCssQuery(string value) {
                    Sync(() => {
                        if (Finder == null) return;
                        Finder.Model.Value = value;
                    });
                }

                private readonly string _stopKey = StringExtension.RandomString(20);

                public override void PageLoaded(string url) {
                    if (Finder == null) return;
                    RunCssSelector();
                }

                private void StopCssSelector() {
                    Tab.Execute(@"window.$KEY && window.$KEY()".Replace(@"$KEY", _stopKey));
                }

                private void RunCssSelector() {
                    Tab.Execute(@"
if (window.$KEY) return;

var CssSelectorGenerator = (function(){var a,b,c=[].indexOf||function(a){for(var b=0,c=this.length;c>b;b++)if(b in this&&this[b]===a)return b;return-1};a=function(){function a(a){null==a&&(a={}),this.options={},this.setOptions(this.default_options),this.setOptions(a)}return a.prototype.default_options={selectors:['id','class','tag','nthchild']},a.prototype.setOptions=function(a){var b,c,d;null==a&&(a={}),c=[];for(b in a)d=a[b],this.default_options.hasOwnProperty(b)?c.push(this.options[b]=d):c.push(void 0);return c},a.prototype.isElement=function(a){return!(1!==(null!=a?a.nodeType:void 0))},a.prototype.getParents=function(a){var b,c;if(c=[],this.isElement(a))for(b=a;this.isElement(b);)c.push(b),b=b.parentNode;return c},a.prototype.getTagSelector=function(a){return this.sanitizeItem(a.tagName.toLowerCase())},a.prototype.sanitizeItem=function(a){var b;return b=a.split('').map(function(a){return':'===a?'\\'+':'.charCodeAt(0).toString(16).toUpperCase()+' ':/[ !""'#$%&'()*+,.\/;<=>?@\[\\\]^`{|}~]/.test(a)?'\\'+a:escape(a).replace(/\%/g,'\\')}),b.join('')},a.prototype.getIdSelector=function(a){var b,c;return b=a.getAttribute('id'),null==b||''===b||/\s/.exec(b)||/^\d/.exec(b)||(c='#'+this.sanitizeItem(b),1!==a.ownerDocument.querySelectorAll(c).length)?null:c},a.prototype.getClassSelectors=function(a){var b,c,d;return d=[],b=a.getAttribute('class'),null!=b&&(b=b.replace(/\s+/g,' '),b=b.replace(/^\s|\s$/g,''),''!==b&&(d=function(){var a,d,e,f;for(e=b.split(/\s+/),f=[],a=0,d=e.length;d>a;a++)c=e[a],f.push('.'+this.sanitizeItem(c));return f}.call(this))),d},a.prototype.getAttributeSelectors=function(a){var b,d,e,f,g,h,i;for(i=[],d=['id','class'],g=a.attributes,e=0,f=g.length;f>e;e++)b=g[e],h=b.nodeName,c.call(d,h)<0&&i.push('['+b.nodeName+'='+b.nodeValue+']');return i},a.prototype.getNthChildSelector=function(a){var b,c,d,e,f,g;if(e=a.parentNode,null!=e)for(b=0,g=e.childNodes,c=0,d=g.length;d>c;c++)if(f=g[c],this.isElement(f)&&(b++,f===a))return b==1?':first-child':':nth-child('+b+')';return null},a.prototype.testSelector=function(a,b){var c,d;return c=!1,null!=b&&''!==b&&(d=a.ownerDocument.querySelectorAll(b),1===d.length&&d[0]===a&&(c=!0)),c},a.prototype.getAllSelectors=function(a){var b;return b={t:null,i:null,c:null,a:null,n:null},c.call(this.options.selectors,'tag')>=0&&(b.t=this.getTagSelector(a)),c.call(this.options.selectors,'id')>=0&&(b.i=this.getIdSelector(a)),c.call(this.options.selectors,'class')>=0&&(b.c=this.getClassSelectors(a)),c.call(this.options.selectors,'attribute')>=0&&(b.a=this.getAttributeSelectors(a)),c.call(this.options.selectors,'nthchild')>=0&&(b.n=this.getNthChildSelector(a)),b},a.prototype.testUniqueness=function(a,b){var c,d;return d=a.parentNode,c=d.querySelectorAll(b),1===c.length&&c[0]===a},a.prototype.testCombinations=function(a,b,c){var d,e,f,g,h,i,j;for(i=this.getCombinations(b),e=0,g=i.length;g>e;e++)if(d=i[e],this.testUniqueness(a,d))return d;if(null!=c)for(j=b.map(function(a){return c+a}),f=0,h=j.length;h>f;f++)if(d=j[f],this.testUniqueness(a,d))return d;return null},a.prototype.getUniqueSelector=function(a){var b,c,d,e,f,g;for(g=this.getAllSelectors(a),e=this.options.selectors,c=0,d=e.length;d>c;c++)switch(f=e[c]){case'id':if(null!=g.i)return g.i;break;case'class':if(null!=g.c&&0!==g.c.length&&(b=this.testCombinations(a,g.c,g.t)))return b;break;case'tag':if(null!=g.t&&this.testUniqueness(a,g.t))return g.t;break;case'attribute':if(null!=g.a&&0!==g.a.length&&(b=this.testCombinations(a,g.a,g.t)))return b;break;case'nthchild':if(null!=g.n)return g.n}return'*'},a.prototype.getSelector=function(a){var b,c,d,e,f,g,h,i,j,k;for(b=[],h=this.getParents(a),d=0,f=h.length;f>d;d++)c=h[d],j=this.getUniqueSelector(c),null!=j&&b.push(j);for(k=[],e=0,g=b.length;g>e&&b[e]!='body';e++){k.unshift(c=b[e]);if(c[0]=='#')break}return k.join(' > ')},a.prototype.getCombinations=function(a){var b,c,d,e,f,g,h;for(null==a&&(a=[]),h=[[]],b=d=0,f=a.length-1;f>=0?f>=d:d>=f;b=f>=0?++d:--d)for(c=e=0,g=h.length-1;g>=0?g>=e:e>=g;c=g>=0?++e:--e)h.push(h[c].concat(a[b]));return h.shift(),h=h.sort(function(a,b){return a.length-b.length}),h=h.map(function(a){return a.join('')})},a}();return a})();

var DomOutline = function(e){e=e||{};var t={},n='$NAMESPACE',o={keyCodes:{BACKSPACE:8,ESC:27,DELETE:46},active:!1,initialized:!1,elements:{}};function d(){var e,t;!0!==o.initialized&&(e='.'+n+'{background:$ACCENT;position:absolute;z-index:1000000;}.'+n+'_label{background:$ACCENT;border-radius:2px;color:#fff;font:bold 12px/12px Helvetica, sans-serif;padding:4px 6px;position:absolute;text-shadow:0 1px 1px rgba(0, 0, 0, 0.25);z-index: 1000001;}',(t=document.getElementsByTagName('head')[0].appendChild(document.createElement('style'))).type='text/css',t.styleSheet?t.styleSheet.cssText=e:t.innerHTML=e,o.initialized=!0)}function l(e,t,n,o,d){e.style.left=t+'px',e.style.top=n+'px',e.style.width=o+'px',e.style.height=d+'px'}function i(e){if(-1===e.target.className.indexOf(n)){t.element=e.target;var d,i,a,s,m=void 0!==window.pageYOffset?window.pageYOffset:(document.documentElement||document.body.parentNode||document.body).scrollTop,c=t.element.getBoundingClientRect(),r=c.top+m;o.elements.label.style.top=Math.max(0,r-20-2,m)+'px',o.elements.label.style.left=Math.max(0,c.left-2)+'px',o.elements.label.textContent=(d=t.element,i=c.width,a=c.height,s=d.tagName.toLowerCase(),d.id&&(s+='#'+d.id),d.className&&(s+=('.'+d.className.trim().replace(/ /g,'.')).replace(/\.\.+/g,'.')),s+' ('+Math.round(i)+'×'+Math.round(a)+')'),l(o.elements.top,c.left-2,r-2,c.width+2+2,2),l(o.elements.bottom,c.left-2,r+c.height,c.width+2+2,2),l(o.elements.left,c.left-2,r,2,c.height),l(o.elements.right,c.left+c.width,r,2,c.height)}}function a(e){e.keyCode!==o.keyCodes.ESC&&e.keyCode!==o.keyCodes.BACKSPACE&&e.keyCode!==o.keyCodes.DELETE||t.stop()}function s(n){n.preventDefault(),n.stopPropagation(),e.onClick.call(t.element,n)}return t.start=function(){d(),!0!==o.active&&(o.active=!0,function(){var e=document.createElement('div');e.classList.add(n+'_label');var t=document.createElement('div'),d=document.createElement('div'),l=document.createElement('div'),i=document.createElement('div');t.classList.add(n),d.classList.add(n),l.classList.add(n),i.classList.add(n);var a=document.body;o.elements.label=a.appendChild(e),o.elements.top=a.appendChild(t),o.elements.bottom=a.appendChild(d),o.elements.left=a.appendChild(l),o.elements.right=a.appendChild(i)}(),document.body.addEventListener('mousemove',i),document.body.addEventListener('keyup',a),setTimeout(function(){document.body.addEventListener('click',s)},50))},t.stop=function(){o.active=!1,function(){for(var e in o.elements){var t=o.elements[e];t.parentNode.removeChild(t)}}(),document.body.removeEventListener('mousemove',i),document.body.removeEventListener('keyup',a),document.body.removeEventListener('click',s)},t};

var outline = DomOutline({ onClick: function (e){
    window.external.SetCssQuery(new CssSelectorGenerator({ selectors: [ 'id', 'class', 'tag', 'nthchild' ] }).getSelector(this));
} });
outline.start();
window.$KEY = outline.stop.bind(outline);

".Replace(@"$ACCENT", AppAppearanceManager.Instance.AccentColor.ToHexString())
 .Replace(@"$NAMESPACE", @"__" + StringExtension.RandomString(20))
 .Replace(@"$KEY", _stopKey));
                }
            }

            async Task<string> ILoader.DownloadAsync(CookieAwareWebClient client, FlexibleLoaderGetPreferredDestinationCallback getPreferredDestination,
                    FlexibleLoaderReportDestinationCallback reportDestination, Func<bool> checkIfPaused, IProgress<long> progress,
                    CancellationToken cancellation) {
                await Task.Delay(500);

                _destinationCallback = getPreferredDestination;
                _reportDestinationCallback = reportDestination;
                _progress = progress;
                _cancellation = cancellation;

                _webBlock = new WebBlock {
                    StartPage = _url,
                    NewWindowsBehavior = NewWindowsBehavior.MultiTab,
                    UserAgent = _source.UserAgent,
                    IsAddressBarVisible = true,
                    DownloadListener = this,
                    Margin = new Thickness(0, 0, 0, 12)
                };

                _finder = new ModsWebFinder {
                    Margin = new Thickness(24, 0, 24, 0),
                    Visibility = Visibility.Collapsed
                };

                DockPanel.SetDock(_finder, Dock.Bottom);
                _webBlock.SetJsBridge<JsBridge>(x => x.Finder = null);

                _message = new BbCodeBlock {
                    BbCode =
                        "Find a download button and click it to install, or [url=\"cmd://setRule\"]describe to CM where download button is[/url] so it could click it automatically.",
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
                    ButtonsRowContentAlignment = HorizontalAlignment.Left
                };

                _resultTask = new TaskCompletionSource<string>();
                _dialog.Buttons = new[] { _dialog.CancelButton };
                _dialog.ShowAndWaitAsync().ContinueWith(t => _resultTask?.TrySetCanceled(), cancellation).Forget();
                cancellation.Register(() => ActionExtension.InvokeInMainThreadAsync(() => _dialog.Close()));
                _resultTask.Task.ContinueWith(t => ActionExtension.InvokeInMainThreadAsync(() => _dialog.Close()), cancellation).Forget();
                return await _resultTask.Task;
            }

            public Task<string> GetDownloadLink(CancellationToken cancellation) {
                return Task.FromResult(_url);
            }

            CommandDictionary ILinkNavigator.Commands { get; set; } = new CommandDictionary();

            event EventHandler<NavigateEventArgs> ILinkNavigator.PreviewNavigate {
                add { }
                remove { }
            }

            private DelegateCommand _testRuleCommand;

            public DelegateCommand TestRuleCommand => _testRuleCommand ?? (_testRuleCommand = new DelegateCommand(() => {

            }, () => !string.IsNullOrWhiteSpace(_finder.Model.Value)).ListenOn(_finder.Model, nameof(_finder.Model.Value)));

            private DelegateCommand _saveRuleCommand;

            public DelegateCommand SaveRuleCommand => _saveRuleCommand ?? (_saveRuleCommand = new DelegateCommand(() => {

            }, () => !string.IsNullOrWhiteSpace(_finder.Model.Value)).ListenOn(_finder.Model, nameof(_finder.Model.Value)));

            private void CancelRuleEditing() {
                _dialog.Buttons = new[] { _dialog.CancelButton };
                _message.Visibility = Visibility.Visible;
                _finder.Visibility = Visibility.Collapsed;
                _webBlock.SetJsBridge<JsBridge>(x => x.Finder = null);
            }

            private void SetRule() {
                _dialog.Buttons = new[] {
                    ModernDialog.CreateExtraDialogButton("Test rule", TestRuleCommand),
                    ModernDialog.CreateExtraDialogButton("Save rule", SaveRuleCommand),
                    ModernDialog.CreateExtraDialogButton("Cancel rule editing", CancelRuleEditing),
                };

                _message.Visibility = Visibility.Hidden;
                _finder.Visibility = Visibility.Visible;
                _webBlock.SetJsBridge<JsBridge>(x => x.Finder = _finder);
            }

            void ILinkNavigator.Navigate(Uri uri, FrameworkElement source, string parameter) {
                if (uri.OriginalString == "cmd://setRule") {
                    SetRule();
                }
            }
        }

        public class ViewModel : NotifyPropertyChanged {
            public WebSource Source { get; }

            public ViewModel(WebSource source) {
                Source = source;
            }
        }

        private void OnFindDownloadButtonClick(object sender, RoutedEventArgs e) { }
    }
}