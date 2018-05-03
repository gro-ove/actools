using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Helpers.Api.TheSetupMarket;
using AcManager.Tools.Managers;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows;

namespace AcManager.Tools.Objects {
    public class RemoteCarSetupObject : AcObjectNew, ICarSetupObject {
        public CarSetupsRemoteSource Source { get; }

        private readonly RemoteSetupInformation _information;

        public RemoteCarSetupObject(RemoteSetupsManager manager, RemoteSetupInformation information) : base(manager, information.Id, true) {
            _information = information;
            Source = manager.Source;
            CarId = _information.CarId;
            TrackId = _information.TrackKunosId;
            Author = _information.Author;
            Version = _information.Version;
            Downloads = _information.Downloads;
            CommunityRating = _information.CommunityRating;
            Trim = _information.Trim;
            BestTime = _information.BestTime;
            _track = new Lazy<TrackObject>(() => TrackId == null ? null : TracksManager.Instance.GetLayoutByKunosId(TrackId)?.MainTrackObject);
        }

        public override void Load() {
            Name = _information.FileName.ApartFromLast(CarSetupObject.FileExtension, StringComparison.OrdinalIgnoreCase);
            CreationDateTime = _information.AddedDateTime ?? DateTime.Now - TimeSpan.FromDays(1e3);
        }

        private readonly Lazy<TrackObject> _track;
        public TrackObject Track => _track.Value;

        public string CarId { get; }
        public string TrackId { get; }
        public string Author { get; }
        public string Version { get; }
        public int Downloads { get; }
        public double? CommunityRating { get; }
        public string Trim { get; }
        public TimeSpan? BestTime { get; }

        public bool IsReadOnly => true;

        private string _loadedData;

        private async Task EnsureDataLoadedInner() {
            try {
                _loadedData = await TheSetupMarketApiProvider.GetSetup(_information.Id) ?? "";
                _iniFile = IniFile.Parse(_loadedData);

                Tyres = _iniFile["TYRES"].GetInt("VALUE", 0);
                Logging.Debug(Tyres);
                OnPropertyChanged(nameof(Values));
            } finally {
                _loaded = true;
                _loadingTask = null;
            }
        }

        public override int CompareTo(AcPlaceholderNew o) {
            return this.CompareTo(o as ICarSetupObject);
        }

        private bool _loaded;
        private Task _loadingTask;

        public Task EnsureDataLoaded() {
            if (_loaded) return Task.Delay(0);
            if (_loadingTask != null) return _loadingTask;
            return _loadingTask = EnsureDataLoadedInner();
        }

        private int? _tyres;

        public int? Tyres {
            get => _tyres;
            set => Apply(value, ref _tyres);
        }

        private IniFile _iniFile = new IniFile();

        public IEnumerable<KeyValuePair<string, double?>> Values =>
                _iniFile.Select(x => new KeyValuePair<string, double?>(x.Key, x.Value.GetDoubleNullable("VALUE")));

        public double? GetValue(string key) {
            if (!_loaded) return null;

            if (!_iniFile.ContainsKey(key)) {
                Logging.Warning($"Key not found: {key}");
            }

            return _iniFile[key].GetDoubleNullable("VALUE");
        }

        public void SetValue(string key, double entryValue) {}

        private DelegateCommand _viewInBrowserCommand;

        public DelegateCommand ViewInBrowserCommand => _viewInBrowserCommand ?? (_viewInBrowserCommand = new DelegateCommand(() => {
            WindowsHelper.ViewInBrowser(_information.Url);
        }, () => _information.Url != null));

        private DelegateCommand _copyUrlCommand;

        public DelegateCommand CopyUrlCommand => _copyUrlCommand ?? (_copyUrlCommand = new DelegateCommand(() => {
            if (_information.Url == null) return;
            ClipboardHelper.SetText(_information.Url);
            Toast.Show("Link copied", "Link to The Setup Market copied to the clipboard");
        }, () => _information.Url != null));

        private AsyncCommand<string> _installCommand;

        public AsyncCommand<string> InstallCommand => _installCommand ?? (_installCommand = new AsyncCommand<string>(async d => {
            await EnsureDataLoaded();
            var filename = FileUtils.EnsureUnique(Path.Combine(AcPaths.GetCarSetupsDirectory(CarId),
                    d ?? (Track?.Id ?? _information.TrackKunosId ?? CarSetupObject.GenericDirectory), _information.FileName));
            FileUtils.EnsureFileDirectoryExists(filename);
            File.WriteAllText(filename, _loadedData);
            Toast.Show("Setup installed", $"Setup {DisplayName} downloaded and installed");
        }));
    }
}