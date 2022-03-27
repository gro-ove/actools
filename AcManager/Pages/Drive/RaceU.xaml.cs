using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using AcManager.Controls.Helpers;
using AcManager.Controls.Presentation;
using AcManager.Controls.UserControls;
using AcManager.Controls.UserControls.Web;
using AcManager.Internal;
using AcManager.Pages.Windows;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Managers;
using AcManager.Tools.Miscellaneous;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.Processes;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using CefSharp;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Serialization;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.Pages.Drive {
    public partial class RaceU {
        private static readonly Uri RaceUUri = new Uri("/Pages/Drive/RaceU.xaml", UriKind.Relative);
        private static RaceU _instance;

        public static void NavigateTo() {
            switch (Application.Current?.MainWindow) {
                case MainWindow mainWindow:
                    mainWindow.NavigateTo(RaceUUri);
                    break;
                case null:
                    MainWindow.NavigateOnOpen(RaceUUri);
                    break;
            }
        }

        private ViewModel Model => (ViewModel)DataContext;

        private static string _navigateToNext = GetRaceUAddress(null);

        private static string GetRaceUAddress(string tag) {
            if (string.IsNullOrWhiteSpace(tag)) {
                return @"https://raceu.net";
            }
            if (tag.IsWebUrl()) {
                return tag;
            }
            return @"https://raceu.net/" + tag;
        }

        public RaceU() {
            this.SetCustomAccentColor(Color.FromArgb(255, 20, 190, 1));

            if (_instance == null && Application.Current?.MainWindow is MainWindow mainWindow) {
                mainWindow.AddHandler(ModernMenu.SelectedChangeEvent, new EventHandler<ModernMenu.SelectedChangeEventArgs>(OnMainWindowLinkChange));
            }
            _instance = this;

            var originalAccentColor = AppAppearanceManager.Instance.AccentColor;
            this.OnActualUnload(() => AppAppearanceManager.Instance.AccentColor = originalAccentColor);

            DataContext = new ViewModel();
            InitializeComponent();

            Browser.StartPage = _navigateToNext;
            Browser.CurrentTabChanged += OnCurrentTabChanged;
            Browser.Tabs.CollectionChanged += OnTabsCollectionChanged;
        }

        private WebTab _previousTab;

        private void OnCurrentTabChanged(object sender, EventArgs e) {
            _previousTab?.UnsubscribeWeak(OnCurrentTabPropertyChanged);
            _previousTab = Browser.CurrentTab;
            _previousTab?.SubscribeWeak(OnCurrentTabPropertyChanged);
            OnAddressChanged();
        }

        private void OnAddressChanged() {
            if (Application.Current?.MainWindow is MainWindow mainWindow) {
                var menu = mainWindow.FindChild<ModernMenu>("PART_Menu");
                if (menu != null) {
                    menu.SelectedLink = menu.SelectedLinkGroup?.Links.FirstOrDefault(x => GetRaceUAddress(x.Tag) == Browser.CurrentTab?.LoadedUrl)
                            ?? menu.SelectedLink;
                }
                UpdateAddressBarVisible();
            }
        }

        private void UpdateAddressBarVisible() {
            Browser.IsAddressBarVisible = Browser.Tabs.Count > 1 || Browser.Tabs.FirstOrDefault()?.LoadedUrl?.StartsWith("https://raceu.net") != true;
        }

        private void OnCurrentTabPropertyChanged(object o, PropertyChangedEventArgs a) {
            if (a.PropertyName == nameof(WebTab.LoadedUrl)) {
                OnAddressChanged();
                UpdateAddressBarVisible();
            }
        }

        private void OnTabsCollectionChanged(object sender, NotifyCollectionChangedEventArgs args) {
            UpdateAddressBarVisible();
        }

        private void OnMainWindowLinkChange(object sender, ModernMenu.SelectedChangeEventArgs args) {
            if (args.SelectedLink?.Source == RaceUUri) {
                _navigateToNext = GetRaceUAddress(args.SelectedLink?.Tag);
                if (_instance?.Browser.Tabs.Count > 0) {
                    _instance?.Browser.Tabs[0].Navigate(_navigateToNext);
                }
            }
        }

        private Button _windowBackButton;
        private IInputElement _windowBackButtonPreviousTarget;

        private void BrowseBack_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = Browser.CurrentTab?.BackCommand.CanExecute(null) == true
                    || NavigationCommands.BrowseBack.CanExecute(null, _windowBackButtonPreviousTarget);
            e.Handled = true;
        }

        private void BrowseBack_Executed(object sender, ExecutedRoutedEventArgs e) {
            if (Browser.CurrentTab?.BackCommand.CanExecute(null) == true) {
                Browser.CurrentTab?.BackCommand.Execute(null);
            } else {
                NavigationCommands.BrowseBack.Execute(null, _windowBackButtonPreviousTarget);
            }
            e.Handled = true;
        }

        private void BrowseForward_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = Browser.CurrentTab?.ForwardCommand.CanExecute(null) == true
                    || NavigationCommands.BrowseForward.CanExecute(null, _windowBackButtonPreviousTarget);
            e.Handled = true;
        }

        private void BrowseForward_Executed(object sender, ExecutedRoutedEventArgs e) {
            if (Browser.CurrentTab?.ForwardCommand.CanExecute(null) == true) {
                Browser.CurrentTab?.ForwardCommand.Execute(null);
            } else {
                NavigationCommands.BrowseForward.Execute(null, _windowBackButtonPreviousTarget);
            }
            e.Handled = true;
        }

        private void Refresh_CanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = true;
            e.Handled = true;
        }

        private void Refresh_Executed(object sender, ExecutedRoutedEventArgs e) {
            Browser.CurrentTab?.RefreshCommand?.Execute(Keyboard.Modifiers.HasFlag(ModifierKeys.Control));
            e.Handled = true;
        }

        private void OnLoaded(object sender, RoutedEventArgs e) {
            _windowBackButton = Application.Current?.MainWindow?.FindChild<Button>("WindowBackButton");
            if (_windowBackButton != null && _windowBackButtonPreviousTarget == null) {
                _windowBackButtonPreviousTarget = _windowBackButton.CommandTarget;
                _windowBackButton.CommandTarget = this;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            if (_windowBackButton != null) {
                _windowBackButton.CommandTarget = _windowBackButtonPreviousTarget;
            }
        }

        public class ViewModel : NotifyPropertyChanged { }

        [UsedImplicitly]
        private class RaceULink {
#pragma warning disable 649
            public string Name;
            public string Url;
            public bool New;
#pragma warning restore 649
        }

        private static void SetRaceULinks(string encoded) {
            if (Application.Current?.MainWindow is MainWindow mainWindow) {
                try {
                    if (string.IsNullOrWhiteSpace(encoded)) {
                        mainWindow.UpdateRaceULinks(new Link[0]);
                    } else {
                        mainWindow.UpdateRaceULinks(JsonConvert.DeserializeObject<List<RaceULink>>(encoded).Select(x => new Link {
                            DisplayName = x.Name,
                            IsNew = x.New,
                            Source = RaceUUri,
                            Tag = x.Url
                        }));
                    }
                } catch (Exception e) {
                    Logging.Warning(e);
                }
            }
        }

        public static void InitializeRaceULinks() {
            SetRaceULinks(CacheStorage.Get<string>(".raceULinks"));
        }

        private static IEnumerable<FileInfo> GetSkinFiles(CarSkinObject skin) {
            return from file in new DirectoryInfo(skin.Location).GetFiles("*.*").OrderBy(x => x.Name) where
                    !string.Equals(file.Name, @"preview.jpg", StringComparison.OrdinalIgnoreCase)
                            && !string.Equals(file.Name, @"livery.png", StringComparison.OrdinalIgnoreCase)
                            && file.Name.EndsWith(@".dds", StringComparison.OrdinalIgnoreCase)
                            || file.Name.EndsWith(@".png", StringComparison.OrdinalIgnoreCase)
                            || file.Name.EndsWith(@".jpg", StringComparison.OrdinalIgnoreCase)
                            || file.Name.EndsWith(@".jpeg", StringComparison.OrdinalIgnoreCase) select file;
        }

        private static async Task<CarSkinObject> GetSkinAsync(string carId, string skinId, IJavascriptCallback callback) {
            var car = await CarsManager.Instance.GetByIdAsync(carId);
            if (car == null) {
                callback?.ExecuteAsync($"Car with ID={carId} is missing", null, null);
                return null;
            }

            var skin = await car.SkinsManager.GetByIdAsync(skinId);
            if (skin == null) {
                callback?.ExecuteAsync($"Skin with ID={skinId} is missing", null, null);
                return null;
            }
            return skin;
        }

        private static async Task UploadBinaryDataAsync(string endpoint, Func<Task<byte[]>> dataCallback, IJavascriptCallback callback) {
            string ret;
            try {
                var response = await HttpClientHolder.Get().PostAsync(endpoint, new ByteArrayContent(await dataCallback()));
                response.EnsureSuccessStatusCode();
                ret = await response.Content.ReadAsStringAsync();
            } catch (Exception e) {
                callback?.ExecuteAsync($"Failed to upload: {e.Message}", null);
                return;
            }
            callback?.ExecuteAsync(null, ret);
        }

        private static async Task<Tuple<string, int>> GetCarSkinChecksumSizeAsync(CarSkinObject skin) {
            var md5 = MD5.Create();
            var hashes = new List<string>();
            var totalSize = 0;
            await Task.Run(() => {
                foreach (var file in GetSkinFiles(skin)) {
                    var data = File.ReadAllBytes(file.FullName);
                    hashes.Add(md5.ComputeHash(data).ToLowerCaseHexString());
                    totalSize += data.Length;
                }
            });

            var finalHash = md5.ComputeHash(Encoding.UTF8.GetBytes(hashes.JoinToString())).ToLowerCaseHexString();
            return Tuple.Create(finalHash, totalSize);
        }

        private static async Task<string> TryGetCarSkinChecksumAsync(CarSkinObject skin) {
            var md5 = MD5.Create();
            var hashes = new List<string>();
            await Task.Run(() => hashes.AddRange(GetSkinFiles(skin).Select(file => File.ReadAllBytes(file.FullName)).Select(
                    data => md5.ComputeHash(data).ToLowerCaseHexString())));
            return md5.ComputeHash(Encoding.UTF8.GetBytes(hashes.JoinToString())).ToLowerCaseHexString();
        }

        private static async Task<bool> ApplyCarSkin(CarObject car, string skinChecksum) {
            return await Task.Run(() => {
                var skinFile = FilesStorage.Instance.GetTemporaryFilename("RaceU", "Skins", car.Id, skinChecksum);
                if (!File.Exists(skinFile)) return false;

                var destination = Path.Combine(car.SkinsDirectory, skinChecksum);
                if (Directory.Exists(destination)) return true;

                FileUtils.EnsureDirectoryExists(destination);
                RaceUTemporarySkinsHelper.MarkForFutherRemoval(destination);

                using (var stream = File.OpenRead(skinFile))
                using (var archive = new ZipArchive(stream)) {
                    archive.ExtractToDirectory(destination);
                }
                return true;
            });
        }

        [PermissionSet(SecurityAction.Demand, Name = "FullTrust"), ComVisible(true)]
        public class RaceUApiProxy : JsGenericProxy {
            public RaceUApiProxy(JsBridgeBase bridge) : base(bridge) { }

            [CanBeNull]
            private CarObject _car;

            [CanBeNull]
            private CarSkinObject _carSkin;

            [CanBeNull]
            private TrackObjectBase _track;

            // RaceU API, v1
            // ReSharper disable InconsistentNaming

            public void getCarSkinsAsync(string carId, IJavascriptCallback callback) {
                ActionExtension.InvokeInMainThreadAsync(async () => {
                    var car = await CarsManager.Instance.GetByIdAsync(carId);
                    if (car == null) {
                        callback?.ExecuteAsync($"Car with ID={carId} is missing", null);
                        return;
                    }
                    await car.SkinsManager.EnsureLoadedAsync();
                    callback?.ExecuteAsync(null, car.EnabledOnlySkins.Select(x => x.Id).ToArray());
                });
            }

            public void getCarSkinSizeAndChecksumAsync(string carId, string skinId, IJavascriptCallback callback) {
                ActionExtension.InvokeInMainThreadAsync(async () => {
                    var skin = await GetSkinAsync(carId, skinId, callback);
                    if (skin == null) return;

                    Tuple<string, int> data;
                    try {
                        data = await GetCarSkinChecksumSizeAsync(skin);
                    } catch (Exception e) {
                        callback?.ExecuteAsync($"Failed to collect files: {e.Message}", null);
                        return;
                    }
                    callback?.ExecuteAsync(null, data.Item1, data.Item2);
                });
            }

            public void filterMissingSkinsAsync(string carId, string[] skinChecksums, IJavascriptCallback callback) {
                ActionExtension.InvokeInMainThreadAsync(async () => {
                    var car = await CarsManager.Instance.GetByIdAsync(carId);
                    if (car == null) {
                        callback?.ExecuteAsync($"Car with ID={carId} is missing", null);
                        return;
                    }

                    var cacheDirectory = FilesStorage.Instance.GetTemporaryDirectory("RaceU", "Skins", carId);
                    skinChecksums = skinChecksums.ApartFrom(Directory.GetFiles(cacheDirectory).Select(Path.GetFileName)).ToArray();

                    if (skinChecksums.Length == 0) {
                        callback?.ExecuteAsync(null, skinChecksums);
                        return;
                    }

                    await car.SkinsManager.EnsureLoadedAsync();
                    var existingSkins = (await car.EnabledOnlySkins.Select(TryGetCarSkinChecksumAsync).WhenAll(4)).NonNull().ToList();
                    callback?.ExecuteAsync(null, skinChecksums.ApartFrom(existingSkins));
                });
            }

            public void prepareSkinAsync(string carId, string skinChecksum, string skinUrl, IJavascriptCallback callback) {
                ActionExtension.InvokeInMainThreadAsync(async () => {
                    var car = await CarsManager.Instance.GetByIdAsync(carId);
                    if (car == null) {
                        callback?.ExecuteAsync($"Car with ID={carId} is missing");
                        return;
                    }

                    var cacheDirectory = FilesStorage.Instance.GetTemporaryDirectory("RaceU", "Skins", carId);
                    var destinationFilename = Path.Combine(cacheDirectory, skinChecksum);
                    if (File.Exists(destinationFilename)) {
                        callback?.ExecuteAsync(null);
                        return;
                    }

                    try {
                        var data = await HttpClientHolder.Get().GetByteArrayAsync(skinUrl);
                        await FileUtils.WriteAllBytesAsync(destinationFilename, data);
                    } catch (Exception e) {
                        callback?.ExecuteAsync($"Failed to download skin: {e.Message}");
                        return;
                    }

                    callback?.ExecuteAsync(null);
                });
            }

            public void applySkinsAsync(string carId, string[] skinChecksums, IJavascriptCallback callback) {
                ActionExtension.InvokeInMainThreadAsync(async () => {
                    var car = await CarsManager.Instance.GetByIdAsync(carId);
                    if (car == null) {
                        callback?.ExecuteAsync($"Car with ID={carId} is missing");
                        return;
                    }

                    using (CarsManager.Instance.IgnoreChanges()) {
                        var applied = (await skinChecksums.Select(x => ApplyCarSkin(car, x)).WhenAll(4)).Count(x => x);
                        if (applied < skinChecksums.Length) {
                            var existingSkins = (await car.EnabledOnlySkins.Select(async x => new {
                                checksum = await TryGetCarSkinChecksumAsync(x),
                                skin = x
                            }).WhenAll(4)).NonNull().ToList();
                            foreach (var entry in existingSkins.Where(x => skinChecksums.Contains(x.checksum))) {
                                var destination = Path.Combine(car.SkinsDirectory, entry.checksum);
                                if (!Directory.Exists(destination)) {
                                    RaceUTemporarySkinsHelper.MarkForFutherRemoval(destination);
                                    Directory.CreateDirectory(destination);
                                    foreach (var file in GetSkinFiles(entry.skin)) {
                                        FileUtils.HardLinkOrCopyRecursive(file.FullName, Path.Combine(destination, file.FullName));
                                    }
                                }
                                ++applied;
                            }
                        }
                        callback?.ExecuteAsync(null, applied);
                    }
                });
            }

            public void uploadCarSkinPreviewAsync(string carId, string skinId, string endpoint, IJavascriptCallback callback) {
                ActionExtension.InvokeInMainThreadAsync(async () => {
                    var skin = await GetSkinAsync(carId, skinId, callback);
                    if (skin == null) return;
                    await UploadBinaryDataAsync(endpoint, () => FileUtils.ReadAllBytesAsync(skin.PreviewImage), callback);
                });
            }

            public void uploadCarSkinLiveryIconAsync(string carId, string skinId, string endpoint, IJavascriptCallback callback) {
                ActionExtension.InvokeInMainThreadAsync(async () => {
                    var skin = await GetSkinAsync(carId, skinId, callback);
                    if (skin == null) return;
                    await UploadBinaryDataAsync(endpoint, () => FileUtils.ReadAllBytesAsync(skin.LiveryImage), callback);
                });
            }

            public void uploadCarSkinAsync(string carId, string skinId, string endpoint, IJavascriptCallback callback) {
                ActionExtension.InvokeInMainThreadAsync(async () => {
                    var skin = await GetSkinAsync(carId, skinId, callback);
                    if (skin == null) return;

                    byte[] data = null;
                    try {
                        await Task.Run(() => {
                            using (var stream = new MemoryStream()) {
                                using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true)) {
                                    foreach (var file in GetSkinFiles(skin)) {
                                        zip.CreateEntryFromFile(file.FullName, file.Name);
                                    }
                                }
                                data = stream.ToArray();
                            }
                        });
                    } catch (Exception e) {
                        callback?.ExecuteAsync($"Failed to compress files: {e.Message}", null);
                        return;
                    }

                    string ret;
                    try {
                        var response = await HttpClientHolder.Get().PostAsync(endpoint, new ByteArrayContent(data));
                        response.EnsureSuccessStatusCode();
                        ret = await response.Content.ReadAsStringAsync();
                    } catch (Exception e) {
                        callback?.ExecuteAsync($"Failed to upload: {e.Message}", null);
                        return;
                    }
                    callback?.ExecuteAsync(null, ret);
                });
            }

            public string getCarSkinPreview(string carId, string skinId) {
                return Sync(() => CarsManager.Instance.GetById(carId)?.GetSkinById(skinId)?.PreviewImage);
            }

            public bool isCspActive() {
                return PatchHelper.IsActive();
            }

            public int getCspBuildNumber() {
                return Sync(() => PatchHelper.GetInstalledBuild().As(-1));
            }

            public void activateCsp() {
                Sync(() => {
                    using (var model = PatchSettingsModel.Create()) {
                        var item = model.Configs?
                                .FirstOrDefault(x => x.FileNameWithoutExtension == "general")?.Sections.GetByIdOrDefault("BASIC")?
                                .GetByIdOrDefault("ENABLED");
                        if (item != null) {
                            item.Value = @"1";
                        }
                    }
                });
            }

            public void installCspBuildAsync(int build, IJavascriptCallback callback) {
                ActionExtension.InvokeInMainThreadAsync(async () => {
                    var versionInfo = PatchUpdater.Instance.Versions.FirstOrDefault(x => x.Build == build);
                    if (versionInfo == null) {
                        callback?.ExecuteAsync($"Version {build} is missing", null);
                        return;
                    }
                    if (await PatchUpdater.Instance.InstallAsync(versionInfo, CancellationToken.None)) {
                        callback?.ExecuteAsync(null, PatchHelper.GetInstalledBuild().As(-1));
                    } else {
                        callback?.ExecuteAsync("Failed to install an update", null);
                    }
                });
            }

            public void _setLinks(string encodedData) {
                CacheStorage.Set(".raceULinks", encodedData);
                ActionExtension.InvokeInMainThread(() => SetRaceULinks(encodedData));
            }

            public bool setCurrentCar(string carId, string skinId = null) {
                _car = CarsManager.Instance.GetById(carId);
                _carSkin = skinId != null ? _car?.GetSkinById(skinId) : null;
                return _car != null;
            }

            public bool setCurrentTrack(string trackId, string layoutId = null) {
                _track = TracksManager.Instance.GetLayoutById(trackId, layoutId);
                return _track != null;
            }

            public void _startOnlineRace(string jsonParams, IJavascriptCallback callback = null) {
                var args = JObject.Parse(jsonParams);

                if (_car == null) {
                    throw new Exception("Car is not set");
                }

                if (_track == null) {
                    throw new Exception("Track is not set");
                }

                var ip = args.GetStringValueOnly("ip") ?? throw new Exception("“ip” parameter is missing");
                var port = args.GetIntValueOnly("port") ?? throw new Exception("“port” parameter is missing");

                var properties = new Game.StartProperties {
                    BasicProperties = new Game.BasicProperties {
                        CarId = _car.Id,
                        TrackId = _track.MainTrackObject.Id,
                        TrackConfigurationId = _track.LayoutId,
                        CarSkinId = _carSkin?.Id ?? _car.SelectedSkin?.Id ?? ""
                    },
                    ModeProperties = new Game.OnlineProperties {
                        Guid = SteamIdHelper.Instance.Value,
                        ServerIp = ip,
                        ServerPort = port,
                        ServerHttpPort = args.GetIntValueOnly("httpPort") ?? throw new Exception("“httpPort” parameter is missing"),
                        Password = args.GetStringValueOnly("password") ?? InternalUtils.GetRaceUPassword(_track.IdWithLayout, ip, port),
                        RequestedCar = _car.Id,
                        CspFeaturesList = args.GetStringValueOnly("cspFeatures"),
                        CspReplayClipUploadUrl = args.GetStringValueOnly("cspReplayClipUploadUrl"),
                    }
                };

                ActionExtension.InvokeInMainThread(async () => {
                    var result = await GameWrapper.StartAsync(properties);
                    callback?.ExecuteAsync(result?.IsNotCancelled);
                }).Ignore();
            }

            public void setThemeAccentColor(string color) {
                ActionExtension.InvokeInMainThread(
                        () => { AppAppearanceManager.Instance.AccentColor = color.ToColor() ?? AppAppearanceManager.Instance.AccentColor; });
            }

            // ReSharper restore InconsistentNaming
        }

        public class RaceUApiBridge : JsBridgeBase {
            public RaceUApiBridge() {
                AcApiHosts.Add(@"raceu.net");
                AcApiHosts.Add(@"localhost:3000");
            }

            public override void PageInject(string url, Collection<string> toInject, Collection<KeyValuePair<string, string>> replacements) {
                base.PageInject(url, toInject, replacements);
                if (IsHostAllowed(url)) {
                    toInject.Add(@"<script>
window.AC = window.external;
window.AC.setLinks = function (links){ return window.AC._setLinks(JSON.stringify(links)); };
window.AC.startOnlineRace = function (options, callback){
    if (arguments.length >= 3) {
        options = { ip: options, port: callback, httpPort: arguments[2] };
        callback = arguments[3];
    }
    window.AC._startOnlineRace(JSON.stringify(options), callback); 
};
</script>");
                }
            }

            public override void PageHeaders(string url, IDictionary<string, string> headers) {
                base.PageHeaders(url, headers);
                if (IsHostAllowed(url)) {
                    headers[@"X-Checksum"] = InternalUtils.GetRaceUChecksum(SteamIdHelper.Instance.Value);
                }
            }

            protected override JsProxyBase MakeProxy() {
                return new RaceUApiProxy(this);
            }
        }

        private void OnWebBlockLoaded(object sender, RoutedEventArgs e) {
            ((WebBlock)sender).SetJsBridge<RaceUApiBridge>();
        }
    }
}