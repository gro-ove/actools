using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using AcManager.Tools.Helpers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Newtonsoft.Json;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local

namespace AcManager.Tools.Managers.Plugins {
    [JsonObject(MemberSerialization.OptIn)]
    public class PluginEntry : NotifyPropertyChanged, IWithId, IProgress<double?> {
        [JsonProperty(PropertyName = @"id")]
        public string Id { get; private set; }

        [JsonProperty(PropertyName = @"name")]
        private string _name;

        [JsonProperty(PropertyName = @"hidden")]
        private bool _hidden;

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
            get { return _name; }
            set {
                if (value == _name) return;
                _name = value;
                OnPropertyChanged();
            }
        }

        public string DisplaySize => LocalizationHelper.ToReadableSize(_size);

        public string KeyEnabled => $"_appAddon__{Id}__enabled";

        public bool AvailableToInstall => !IsInstalled && !InstallationInProgress;

        public bool IsInstalled => _installedVersion != null;

        public bool IsEnabled {
            get { return ValuesStorage.GetBool(KeyEnabled, true); }
            set {
                if (value == IsEnabled) return;
                if (value) {
                    ValuesStorage.Remove(KeyEnabled);
                } else {
                    ValuesStorage.Set(KeyEnabled, false);
                }

                PluginsManager.Instance.OnPluginEnabled(this, value);
            }
        }

        public bool IsAvailable => !BuildInformation.AppVersion.IsVersionOlderThan(AppVersion);

        /// <summary>
        /// Addon is installed and enabled.
        /// </summary>
        public bool IsReady => IsInstalled && IsEnabled;

        private bool _isInstalling;

        public bool IsInstalling {
            get { return _isInstalling; }
            set {
                if (value == _isInstalling) return;
                _isInstalling = value;
                OnPropertyChanged();
                _installCommand?.OnCanExecuteChanged();
            }
        }

        public bool HasUpdate => IsInstalled && InstalledVersion != Version;

        public string InstalledVersion {
            get { return _installedVersion; }
            set {
                if (value == _installedVersion) return;
                _installedVersion = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsInstalled));
                OnPropertyChanged(nameof(AvailableToInstall));
                _installCommand?.OnCanExecuteChanged();
            }
        }

        public string Description {
            get { return _description; }
            set {
                if (value == _description) return;
                _description = value;
                OnPropertyChanged();
            }
        }

        public bool IsHidden {
            get { return _hidden; }
            set {
                if (Equals(value, _hidden)) return;
                _hidden = value;
                OnPropertyChanged();
            }
        }

        public string Version {
            get { return _version; }
            set {
                if (value == _version) return;
                _version = value;
                OnPropertyChanged();
            }
        }

        private bool _installationInProgress;

        public bool InstallationInProgress {
            get { return _installationInProgress; }
            set {
                if (Equals(value, _installationInProgress)) return;
                _installationInProgress = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(AvailableToInstall));
            }
        }

        private bool _downloadProgressIndeterminate;

        public bool DownloadProgressIndeterminate {
            get { return _downloadProgressIndeterminate; }
            set {
                if (Equals(value, _downloadProgressIndeterminate)) return;
                _downloadProgressIndeterminate = value;
                OnPropertyChanged();
            }
        }

        private double _downloadProgress;

        public double DownloadProgress {
            get { return _downloadProgress; }
            set {
                if (Equals(value, _downloadProgress)) return;
                _downloadProgress = value;
                OnPropertyChanged();
            }
        }

        public void Report(double? value) {
            var v = value ?? 0d;
            DownloadProgressIndeterminate = Equals(v, 0d);
            DownloadProgress = v;
        }

        private PluginEntry(string id) {
            Id = id;
            AppVersion = "0";
        }

        [JsonConstructor, UsedImplicitly]
        private PluginEntry() { }

        private ICommandExt _installCommand;

        public ICommand InstallCommand => _installCommand ??
                (_installCommand = new AsyncCommand(Install, () => !IsInstalled && !IsInstalling));

        private CancellationTokenSource _cancellation;

        private async Task Install() {
            using (_cancellation = new CancellationTokenSource()) {
                Report(0d);

                InstallationInProgress = true;

                try {
                    await PluginsManager.Instance.InstallPlugin(this, this, _cancellation.Token);
                } finally {
                    InstallationInProgress = false;
                }
            }
            _cancellation = null;
        }

        private ICommandExt _cancellationCommand;

        public ICommand CancellationCommand => _cancellationCommand ?? (_cancellationCommand = new DelegateCommand(() => {
            _cancellation?.Cancel();
        }));

        public bool IsAllRight => Id != null && Name != null && Version != null;

        [JsonIgnore]
        public string Directory => PluginsManager.Instance.GetPluginDirectory(Id);
        
        public string GetFilename([Localizable(false)] string fileId) {
            return PluginsManager.Instance.GetPluginFilename(Id, fileId);
        }
    }
}
