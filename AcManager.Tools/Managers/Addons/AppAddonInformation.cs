using System.Windows.Input;
using AcManager.Tools.Helpers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Newtonsoft.Json;

// ReSharper disable AutoPropertyCanBeMadeGetOnly.Local

namespace AcManager.Tools.Managers.Addons {
    [JsonObject(MemberSerialization.OptIn)]
    public class AppAddonInformation : NotifyPropertyChanged, IWithId {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; private set; }

        [JsonProperty(PropertyName = "name")]
        private string _name;

        [JsonProperty(PropertyName = "hidden")]
        private bool _hidden;

        [JsonProperty(PropertyName = "description")]
        private string _description;

        [JsonProperty(PropertyName = "version")]
        private string _version;

        [JsonProperty(PropertyName = "installedVersion")]
        private string _installedVersion;

        [JsonProperty(PropertyName = "size")]
#pragma warning disable 649
        private int _size;
#pragma warning restore 649

        [JsonProperty(PropertyName = "appVersion")]
        public string AppVersion { get; private set; }

        public string Name {
            get { return _name; }
            set {
                if (value == _name) return;
                _name = value;
                OnPropertyChanged();
            }
        }

        public string DisplaySize => LocalizationHelper.ReadableSize(_size);

        public string KeyEnabled => "_appAddon__" + Id + "__enabled";

        public bool IsInstalled => _installedVersion != null;

        public bool IsEnabled {
            get { return ValuesStorage.GetBool(KeyEnabled); }
            set {
                if (value == IsEnabled) return;
                ValuesStorage.Set(KeyEnabled, value);
                AppAddonsManager.Instance.OnAddonEnabled(this, value);
            }
        }

        public bool IsAvailable => !BuildInformation.AppVersion.IsVersionOlderThan(AppVersion);

        /// <summary>
        /// Addon is installed and enabled.
        /// </summary>
        public bool IsReady => IsInstalled && IsEnabled;

        public bool IsInstalling {
            get { return _isInstalling; }
            set {
                if (value == _isInstalling) return;
                _isInstalling = value;
                OnPropertyChanged();
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

        private AppAddonInformation(string id) {
            Id = id;
            AppVersion = "0";
        }

        [JsonConstructor]
        [UsedImplicitly]
        private AppAddonInformation() { }

        private ICommand _installCommand;
        private bool _isInstalling;

        public ICommand InstallCommand => _installCommand ??
                (_installCommand = new RelayCommand(x => InstallAddon(), x => CanInstall()));

        public bool IsAllRight => Id != null && Name != null && Version != null;

        [JsonIgnore]
        public string Directory => AppAddonsManager.Instance.GetAddonDirectory(Id);
        
        public string GetFilename(string fileId) {
            return AppAddonsManager.Instance.GetAddonFilename(Id, fileId);
        }

        private bool CanInstall() {
            return !IsInstalled && !IsInstalling;
        }

        private void InstallAddon() {
            AppAddonsManager.Instance.InstallAppAddon(this).Forget();
        }
    }
}
