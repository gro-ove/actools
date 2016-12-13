using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Helpers.Api.Kunos;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Online {
    public partial class ServerEntry {
        private class InvariantOriginIdComparer : IComparer<string> {
            public static readonly InvariantOriginIdComparer Instance = new InvariantOriginIdComparer();

            public int Compare(string x, string y) {
                return string.Compare(x, y, StringComparison.Ordinal);
            }
        }

        /// <summary>
        /// Sorted list of IDs.
        /// </summary>
        private readonly SortedList<string> _origins = new SortedList<string>(4, InvariantOriginIdComparer.Instance);

        private string _originsString;

        public string OriginsString => _originsString ?? (_originsString = _origins.JoinToString(','));

        public IEnumerable<string> GetOriginsIds() {
            return _origins;
        }

        public void SetOrigin(string key) {
            if (!_origins.Contains(key)) {
                _originsString = null;
                _origins.Add(key);
                OnPropertyChanged(nameof(OriginsString));

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
                        SetIsFavorited(true);
                        break;
                    case FileBasedOnlineSources.RecentKey:
                        WasUsedRecently = true;
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
                _originsString = null;
                OnPropertyChanged(nameof(OriginsString));

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
                        SetIsFavorited(false);
                        break;
                    case FileBasedOnlineSources.RecentKey:
                        WasUsedRecently = false;
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

        /// <summary>
        /// Throws an exception.
        /// </summary>
        /// <returns></returns>
        [ItemNotNull]
        private Task<ServerInformation> GetInformationDirectly() {
            return KunosApiProvider.GetInformationDirectAsync(Ip, PortHttp);
        }

        /// <summary>
        /// Throws an exception.
        /// </summary>
        /// <returns></returns>
        [ItemCanBeNull]
        private Task<ServerInformation> GetInformation(bool nonDirectOnly = false) {
            if (!SettingsHolder.Online.LoadServerInformationDirectly && IsFullyLoaded) {
                if (OriginsFromKunos) {
                    if (SteamIdHelper.Instance.Value == null) {
                        throw new InformativeException(ToolsStrings.Common_SteamIdIsMissing);
                    }

                    return KunosApiProvider.GetInformationAsync(Ip, Port);
                }
            }

            return nonDirectOnly ? null : GetInformationDirectly();
        }

        #region Kunos-specific
        private bool _originsFromKunos;

        public bool OriginsFromKunos {
            get { return _originsFromKunos; }
            private set {
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
            private set {
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
            private set {
                if (Equals(value, _originsFromMinorating)) return;
                _originsFromMinorating = value;
                OnPropertyChanged();
            }
        }
        #endregion

        #region Lists-related
        private bool _isFavorited;

        public bool IsFavorited {
            get { return _isFavorited; }
            set {
                if (Equals(value, _isFavorited)) return;
                // SetIsFavorited(value);

                if (value) {
                    FileBasedOnlineSources.AddToList(FileBasedOnlineSources.FavoritesKey, this);
                } else {
                    FileBasedOnlineSources.RemoveFromList(FileBasedOnlineSources.FavoritesKey, this);
                }
            }
        }

        /// <summary>
        /// For internal use.
        /// </summary>
        /// <param name="value">New value.</param>
        private void SetIsFavorited(bool value) {
            if (Equals(value, _isFavorited)) return;
            _isFavorited = value;
            OnPropertyChanged(nameof(IsFavorited));
        }

        private bool _wasUsedRecently;

        public bool WasUsedRecently {
            get { return _wasUsedRecently; }
            private set {
                if (Equals(value, _wasUsedRecently)) return;
                _wasUsedRecently = value;
                OnPropertyChanged();
            }
        }
        #endregion
    }
}
