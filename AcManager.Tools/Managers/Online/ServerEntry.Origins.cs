using System.Collections.Generic;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Helpers.Api.Kunos;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Online {
    public partial class ServerEntry {
        private readonly List<string> _origins = new List<string>(2);

        public string Origins => _origins.JoinToString(',');

        public void SetOrigin(string key) {
            if (!_origins.Contains(key)) {
                _origins.Add(key);
                OnPropertyChanged(nameof(Origins));

                switch (key) {
                    case LanOnlineSource.Key:
                        OriginsFromLan = true;
                        break;
                    case KunosOnlineSource.Key:
                        OriginsFromKunos = true;
                        break;
                    case MinoratingOnlineSource.Key:
                        OriginsFromMinorating = true;
                        break;
                    case FileBasedOnlineSources.FavoritesKey:
                        IsFavorited = true;
                        break;
                }
            }
        }

        /// <summary>
        /// Remove origin from origins list.
        /// </summary>
        /// <param name="key">Origin (aka source) key.</param>
        /// <returns>True if it was the only origin and now its list is empty.</returns>
        public bool RemoveOrigin(string key) {
            if (_origins.Remove(key)) {
                OnPropertyChanged(nameof(Origins));

                switch (key) {
                    case LanOnlineSource.Key:
                        OriginsFromLan = false;
                        break;
                    case KunosOnlineSource.Key:
                        OriginsFromKunos = false;
                        break;
                    case MinoratingOnlineSource.Key:
                        OriginsFromMinorating = false;
                        break;
                    case FileBasedOnlineSources.FavoritesKey:
                        IsFavorited = false;
                        break;
                }
            }

            return _origins.Count == 0;
        }

        public bool OriginsFrom(string source) {
            for (var j = 0; j < _origins.Count; j++) {
                if (source == _origins[j]) return true;
            }
            return false;
        }

        public bool OriginsFrom(string[] sources) {
            for (var i = 0; i < sources.Length; i++) {
                if (OriginsFrom(sources[i])) return true;
            }
            return false;
        }

        [ItemCanBeNull]
        private Task<ServerInformation> GetInformationDirectly() {
            return KunosApiProvider.TryToGetInformationDirectAsync(Ip, PortHttp);
        }

        [ItemCanBeNull]
        private Task<ServerInformation> GetInformation(bool nonDirectOnly = false) {
            if (!SettingsHolder.Online.LoadServerInformationDirectly && IsFullyLoaded) {
                if (OriginsFromKunos) {
                    if (SteamIdHelper.Instance.Value == null) {
                        throw new InformativeException(ToolsStrings.Common_SteamIdIsMissing);
                    }

                    return Task.Run(() => KunosApiProvider.TryToGetInformation(Ip, Port));
                }
            }

            return nonDirectOnly ? null : GetInformationDirectly();
        }

        #region Kunos-specific
        private bool _originsFromKunos;

        public bool OriginsFromKunos {
            get { return _originsFromKunos; }
            set {
                if (Equals(value, _originsFromKunos)) return;
                _originsFromKunos = value;
                OnPropertyChanged();
            }
        }
        #endregion

        #region LAN-related
        private bool _originsFromLan;

        public bool OriginsFromLan {
            get { return _originsFromLan; }
            set {
                if (Equals(value, _originsFromLan)) return;
                _originsFromLan = value;
                OnPropertyChanged();
            }
        }
        #endregion

        #region Minorating-related
        private bool _originsFromMinorating;

        public bool OriginsFromMinorating {
            get { return _originsFromMinorating; }
            set {
                if (Equals(value, _originsFromMinorating)) return;
                _originsFromMinorating = value;
                OnPropertyChanged();
            }
        }
        #endregion

        #region Favorites-related
        private bool _isFavorited;

        public bool IsFavorited {
            get { return _isFavorited; }
            set {
                if (Equals(value, _isFavorited)) return;
                _isFavorited = value;
                OnPropertyChanged();
            }
        }
        #endregion
    }
}
