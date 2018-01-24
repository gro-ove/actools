using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.RegularExpressions;
using AcManager.Internal;
using AcManager.Tools.Helpers;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using Newtonsoft.Json;

namespace AcManager.Pages.Miscellaneous {
    public partial class ModsWebBrowser {
        public ModsWebBrowser() {
            DataContext = new ViewModel();
            InitializeComponent();
        }

        private ViewModel Model => (ViewModel)DataContext;

        [JsonObject(MemberSerialization.OptIn)]
        public sealed class WebSource : Displayable {
            [JsonConstructor]
            private WebSource(string name, string domainName) {

            }

            public WebSource(string url) {
                DisplayName = url;
                Url = url;
                Favicon = url.Split('/').Take(3).JoinToString('/').TrimEnd('/') + @"/favicon.ico";
                UpdateDisplayNameCommand.Execute();
            }

            private AsyncCommand _updateDisplayNameCommand;

            public AsyncCommand UpdateDisplayNameCommand => _updateDisplayNameCommand ?? (_updateDisplayNameCommand = new AsyncCommand(async () => {
                using (var wc = KillerOrder.Create(new CookieAwareWebClient(), TimeSpan.FromSeconds(10)))
                using (wc.Victim.MaskAsCommonBrowser()) {
                    var data = await wc.Victim.DownloadStringTaskAsync(Url);

                    var name = Regex.Match(data, @"<title>([^<]+)</title>").Groups[1].Value;
                    if (!string.IsNullOrWhiteSpace(name)) {
                        DisplayName = name;
                    }

                    var favicon = Regex.Match(data, @"<link\s+rel=""icon""\s+href=""([^""]+)").Groups[1].Value;
                    if (!string.IsNullOrWhiteSpace(favicon)) {
                        Favicon = favicon;
                    }
                }
            }));

            private bool _isEnabled;

            [JsonProperty("enabled")]
            public bool IsEnabled {
                get => _isEnabled;
                set {
                    if (Equals(value, _isEnabled)) return;
                    _isEnabled = value;
                    OnPropertyChanged();
                }
            }

            private bool _isFavourite;

            [JsonProperty("favourite")]
            public bool IsFavourite {
                get => _isFavourite;
                set {
                    if (Equals(value, _isFavourite)) return;
                    _isFavourite = value;
                    OnPropertyChanged();
                }
            }

            private string _favicon;

            [JsonProperty("favicon")]
            public string Favicon {
                get => _favicon;
                set {
                    if (Equals(value, _favicon)) return;
                    _favicon = value;
                    OnPropertyChanged();
                }
            }

            private string _url;

            [JsonProperty("url")]
            public string Url {
                get => _url;
                set {
                    if (Equals(value, _url)) return;
                    _url = value;
                    OnPropertyChanged();
                }
            }

            private bool _captureDownloads;

            [JsonProperty("captureDownloads")]
            public bool CaptureDownloads {
                get => _captureDownloads;
                set {
                    if (Equals(value, _captureDownloads)) return;
                    _captureDownloads = value;
                    OnPropertyChanged();
                }
            }
        }

        public class ViewModel : NotifyPropertyChanged, IComparer<WebSource> {
            public ChangeableObservableCollection<WebSource> WebSources { get; }

            private WebSource _selectedSource;

            public WebSource SelectedSource {
                get => _selectedSource;
                set {
                    if (Equals(value, _selectedSource)) return;
                    _selectedSource = value;
                    OnPropertyChanged();
                }
            }

            public ViewModel() {
                WebSources = new ChangeableObservableCollection<WebSource>();
                WebSources.ItemPropertyChanged += OnItemPropertyChanged;
                SelectedSource = WebSources.FirstOrDefault();
            }

            private static readonly Lazier<IReadOnlyCollection<string>> FoundDomains = Lazier.CreateAsync(async () => {
                using (WaitingDialog.Create("Wait a second…"))
                using (var wc = KillerOrder.Create(new CookieAwareWebClient(), TimeSpan.FromSeconds(5)))
                using (wc.Victim.MaskAsCommonBrowser()) {
                    var data = await wc.Victim.DownloadStringTaskAsync("https://duckduckgo.com/html/?q=assetto+corsa+mods");
                    return (IReadOnlyCollection<string>)Regex.Matches(data, @"result__a"" href=""([^""]+)""").OfType<Match>().Select(
                            x => Uri.UnescapeDataString(
                                    Enumerable.ElementAtOrDefault(x.Groups[1].Value.Split(new[] { "uddg=" }, StringSplitOptions.RemoveEmptyEntries), 1)?.Split(new[] { "&amp;" }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "")
                            ).Take(10).ToList();
                }
            });

            private AsyncCommand _addNewSourceCommand;

            public AsyncCommand AddNewSourceCommand => _addNewSourceCommand ?? (_addNewSourceCommand = new AsyncCommand(async () => {
                var url = Prompt.Show("Enter URL for new source", "Add new source", required: true, maxLength: 80, watermark: "?",
                        suggestions: await FoundDomains.GetValueAsync());
                var newSource = new WebSource(url);
                WebSources.AddSorted(newSource, this);
                SelectedSource = newSource;
            }));

            private void OnItemPropertyChanged(object sender, PropertyChangedEventArgs e) {
            }

            public int Compare(WebSource x, WebSource y) {
                return x?.DisplayName?.CompareTo(y?.DisplayName) ?? 0;
            }
        }
    }
}