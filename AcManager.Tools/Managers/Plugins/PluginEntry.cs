using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcTools;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Newtonsoft.Json;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local

namespace AcManager.Tools.Managers.Plugins {
    [JsonObject(MemberSerialization.OptIn)]
    public class PluginEntry : NotifyPropertyChanged, IWithId {
        [Localizable(false)]
        public static readonly Tuple<string, string>[] SupportedVersions = {
            Tuple.Create("Magick", ""),
            Tuple.Create("Awesomium", ""),
            Tuple.Create("CefSharp", ""),
            Tuple.Create("CefSharp-63.0.0-x86", ""),
            Tuple.Create("CefSharp-69.0.0-x86", ""),
            Tuple.Create("SSE", "1.4.2.1"),
            Tuple.Create(KnownPlugins.SevenZip, "17.0.1"),
        };

        [JsonProperty(PropertyName = @"id")]
        public string Id { get; private set; }

        public string GroupId => Id.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries)[0];

        [JsonProperty(PropertyName = @"name")]
        private string _name;

        [JsonProperty(PropertyName = @"hidden")]
        private bool _hidden;

        [JsonProperty(PropertyName = @"recommended")]
        private bool? _isRecommended;

        [JsonProperty(PropertyName = @"platform"), CanBeNull]
        private string _platform;

        [JsonProperty(PropertyName = @"description")]
        private string _description;

        [JsonProperty(PropertyName = @"version")]
        private string _version;

        [JsonProperty(PropertyName = @"installedVersion")]
        private string _installedVersion;

        [JsonProperty(PropertyName = @"size")]
#pragma warning disable 649
        private int _size;
#pragma warning restore 649

        [Localizable(false),JsonProperty(PropertyName = @"appVersion")]
        public string AppVersion { get; private set; }

        public string Name {
            get => _name;
            set => Apply(value, ref _name);
        }

        public string DisplaySize => LocalizationHelper.ToReadableSize(_size);
        public string KeyEnabled => $"_appAddon__{Id}__enabled";

        public bool AvailableToInstall => !IsInstalled && !IsInstalling;
        public bool IsInstalled => _installedVersion != null;

        public bool IsEnabled {
            get => ValuesStorage.Get(KeyEnabled, true) && !IsObsolete;
            set {
                if (value == IsEnabled) return;
                if (value) {
                    ValuesStorage.Remove(KeyEnabled);
                } else {
                    ValuesStorage.Set(KeyEnabled, false);
                }

                PluginsManager.Instance.OnPluginEnabled(this, value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsReady));
            }
        }

        public bool IsAvailable => !BuildInformation.AppVersion.IsVersionOlderThan(AppVersion);

        public bool CanWork => IsAvailable && !IsObsolete;

        /// <summary>
        /// Addon is installed and enabled.
        /// </summary>
        public bool IsReady => IsInstalled && IsEnabled;

        public bool HasUpdate => IsInstalled && Version.IsVersionNewerThan(InstalledVersion);

        public string InstalledVersion {
            get => _installedVersion;
            set {
                if (value == _installedVersion) return;
                _installedVersion = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsInstalled));
                OnPropertyChanged(nameof(AvailableToInstall));
                OnPropertyChanged(nameof(HasUpdate));
                _installCommand?.RaiseCanExecuteChanged();

                UpdateObsolete();
            }
        }

        public string Description {
            get => _description;
            set => Apply(value, ref _description);
        }

        public bool IsRecommended {
            get => _isRecommended ?? (_isRecommended = Id != "Awesomium" && Id != "VLC").Value;
            set => Apply(value, ref _isRecommended);
        }

        public bool IsHidden {
            get => _hidden;
            set => Apply(value, ref _hidden);
        }

        public string Version {
            get => _version;
            set => Apply(value, ref _version);
        }

        public string Platform {
            get => _platform;
            set => Apply(value, ref _platform);
        }

        public bool PlatformFits => _platform == null || _platform == BuildInformation.Platform;

        private PluginEntry(string id) {
            Id = id;
            AppVersion = "0";
        }

        [JsonConstructor, UsedImplicitly]
        private PluginEntry(string id, string version) {
            Id = id;
            Version = version;
            UpdateObsolete();
        }

        private bool _isObsolete;

        public bool IsObsolete {
            get => _isObsolete;
            set {
                if (value == _isObsolete) return;
                _isObsolete = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsEnabled));
                OnPropertyChanged(nameof(CanWork));
            }
        }

        private void UpdateObsolete() {
            var supported = SupportedVersions.FirstOrDefault(x => x.Item1 == Id);
            IsObsolete = supported != null && (string.IsNullOrEmpty(supported.Item2) || supported.Item2.IsVersionNewerThan(InstalledVersion ?? Version));
        }

        private bool _isInstalling;

        public bool IsInstalling {
            get => _isInstalling;
            set => Apply(value, ref _isInstalling);
        }

        private AsyncCommand _installCommand;

        public AsyncCommand InstallCommand => _installCommand ??
                (_installCommand = new AsyncCommand(Install, () => !IsInstalled || HasUpdate));

        private AsyncProgressEntry _progress;

        public AsyncProgressEntry Progress {
            get => _progress;
            set => Apply(value, ref _progress);
        }

        private CancellationTokenSource _cancellationTokenSource;

        private DelegateCommand _cancelCommand;

        public DelegateCommand CancelCommand => _cancelCommand ?? (_cancelCommand = new DelegateCommand(() => {
            _cancellationTokenSource?.Cancel();
        }, () => _cancellationTokenSource != null));

        private async Task Install() {
            using (var cts = new CancellationTokenSource()) {
                _cancellationTokenSource = cts;
                _cancelCommand?.RaiseCanExecuteChanged();
                Progress = AsyncProgressEntry.FromStringIndetermitate("Downloading…");
                await PluginsManager.Instance.InstallPlugin(this, new Progress<AsyncProgressEntry>(v => {
                    if (ReferenceEquals(cts, _cancellationTokenSource)) {
                        // Trim everything after “,” to show progress on the button itself
                        // TODO: Find a better solution?
                        var index = v.Message.IndexOf(',');
                        Progress = new AsyncProgressEntry(index != -1 ? v.Message.Substring(0, index) : v.Message, v.Progress);
                    }
                }), cts.Token);
                if (ReferenceEquals(cts, _cancellationTokenSource)) {
                    Progress = AsyncProgressEntry.Ready;
                    _cancellationTokenSource = null;
                    _cancelCommand?.RaiseCanExecuteChanged();
                }
            }
        }

        private async Task Install(IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            IsInstalling = true;
            try {
                await PluginsManager.Instance.InstallPlugin(this, progress, cancellation);
            } finally {
                IsInstalling = false;
            }
        }

        public bool IsAllRight => Id != null && Name != null && Version != null && PlatformFits;

        [JsonIgnore]
        public string Directory => PluginsManager.Instance.GetPluginDirectory(Id);

        public string GetFilename([Localizable(false)] string fileId) {
            return PluginsManager.Instance.GetPluginFilename(Id, fileId);
        }
    }
}
