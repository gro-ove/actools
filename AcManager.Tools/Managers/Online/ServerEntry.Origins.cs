using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Helpers.Api.Kunos;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
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
        private readonly SortedList<string> _origins = new SortedList<string>(2, InvariantOriginIdComparer.Instance);

        /// <summary>
        /// Another list, not origins, but references.
        /// </summary>
        private readonly SortedList<string> _references = new SortedList<string>(4, InvariantOriginIdComparer.Instance);

        /// <summary>
        /// Server is excluded if added in any excluded reference.
        /// </summary>
        private readonly List<string> _excludedReferences = new List<string>(2);

        private string _originsString;

        public string OriginsString => _originsString ?? (_originsString = _origins.JoinToString(','));

        private string _referencesString;

        public string ReferencesString => _referencesString ?? (_referencesString = _references.JoinToString(','));

        public IEnumerable<string> GetReferencesIds() {
            return _references;
        }

        public void SetOrigin(string key) {
            if (!_origins.Contains(key)) {
                _originsString = null;
                _origins.Add(key);
                OnPropertyChanged(nameof(OriginsString));
                SetReference(key);
            }
        }

        /// <summary>
        /// Remove origin from origins list.
        /// </summary>
        /// <param name="key">Origin (aka source) key.</param>
        /// <returns>True if it was the only origin and now its list is empty.</returns>
        public bool RemoveOrigin(string key) {
            RemoveReference(key);

            if (_origins.Remove(key)) {
                _originsString = null;
                OnPropertyChanged(nameof(OriginsString));
            }

            return _origins.Count == 0;
        }

        private void AddExcluded(string key) {
            if (!_excludedReferences.Contains(key)) {
                _excludedReferences.Add(key);
                SetIsExcluded(true);
            }
        }

        private void RemoveExcluded(string key) {
            if (_excludedReferences.Remove(key) && _excludedReferences.Count == 0) {
                SetIsExcluded(false);
            }
        }

        public void UpdateExcluded(string key, bool newValue) {
            if (newValue) {
                AddExcluded(key);
            } else {
                RemoveExcluded(key);
            }
        }

        public void SetReference(string key) {
            if (!_references.Contains(key)) {
                _referencesString = null;
                _references.Add(key);
                OnPropertyChanged(nameof(ReferencesString));

                switch (key) {
                    case LanOnlineSource.Key:
                        FromLan = true;
                        break;
                    case KunosOnlineSource.Key:
                        FromKunosList = true;
                        break;
                    case MinoratingOnlineSource.Key:
                        FromMinoratingList = true;
                        break;
                    case FileBasedOnlineSources.FavouritesKey:
                        SetIsFavourited(true);
                        break;
                    case FileBasedOnlineSources.RecentKey:
                        WasUsedRecently = true;
                        break;
                    case FileBasedOnlineSources.HiddenKey:
                        AddExcluded(key);
                        break;
                    default:
                        if (FileBasedOnlineSources.Instance.IsSourceExcluded(key)) {
                            AddExcluded(key);
                        }
                        break;
                }
            }
        }

        public void SetReferences(IEnumerable<string> keys) {
            foreach (var key in keys) {
                SetReference(key);
            }
        }

        public void RemoveReference(string key) {
            if (_references.Remove(key)) {
                _referencesString = null;
                OnPropertyChanged(nameof(ReferencesString));

                switch (key) {
                    case LanOnlineSource.Key:
                        FromLan = false;
                        break;
                    case KunosOnlineSource.Key:
                        FromKunosList = false;
                        break;
                    case MinoratingOnlineSource.Key:
                        FromMinoratingList = false;
                        break;
                    case FileBasedOnlineSources.FavouritesKey:
                        SetIsFavourited(false);
                        break;
                    case FileBasedOnlineSources.RecentKey:
                        WasUsedRecently = false;
                        break;
                    case FileBasedOnlineSources.HiddenKey:
                        RemoveExcluded(key);
                        break;
                    default:
                        if (FileBasedOnlineSources.Instance.IsSourceExcluded(key)) {
                            RemoveExcluded(key);
                        }
                        break;
                }
            }
        }

        public bool ReferencedFrom(string source) {
            return _references.Contains(source);
        }

        public bool ReferencedFrom(string[] sources) {
            for (var i = 0; i < sources.Length; i++) {
                if (ReferencedFrom(sources[i])) return true;
            }
            return false;
        }

        /// <summary>
        /// Throws an exception.
        /// </summary>
        /// <returns></returns>
        [ItemNotNull]
        private Task<ServerInformationComplete> GetInformationDirectly() {
            return KunosApiProvider.GetInformationDirectAsync(Ip, PortHttp);
        }

        private Tuple<ServerInformationExtended, DateTime> _extendedDataLastModified;

        /// <summary>
        /// Throws an exception.
        /// </summary>
        /// <returns></returns>
        [ItemNotNull]
        private async Task<ServerInformationExtended> GetExtendedInformationDirectly() {
            var result = await KunosApiProvider.GetExtendedInformationDirectAsync(Ip,
                    DetailsPort ?? throw new Exception("Extended port is not set"),
                    _extendedDataLastModified?.Item2);

            if (result.Item1 != null) {
                _extendedDataLastModified = result;
            } else if (_extendedDataLastModified != null) {
                _extendedDataLastModified = Tuple.Create(_extendedDataLastModified.Item1, result.Item2);
            }

            Logging.Debug(result.Item1 == null ? "Cached information used!" : "New information loaded: " + result.Item2);
            if (result.Item1 != null) return result.Item1;

            var r = _extendedDataLastModified?.Item1;
            if (r == null) throw new Exception("Server went mad");
            if (r.UntilLocal != default(DateTime)) {
                r.TimeLeft = Math.Max((long)(r.UntilLocal - DateTime.Now).TotalSeconds, 0);
            }

            return r;
        }

        /// <summary>
        /// Throws an exception.
        /// </summary>
        /// <returns></returns>
        [ItemCanBeNull]
        private Task<ServerInformationComplete> GetInformation(bool nonDirectOnly = false) {
            if (!SettingsHolder.Online.LoadServerInformationDirectly && IsFullyLoaded) {
                if (FromKunosList) {
                    if (SteamIdHelper.Instance.Value == null) {
                        throw new InformativeException(ToolsStrings.Common_SteamIdIsMissing);
                    }

                    return KunosApiProvider.GetInformationAsync(Ip, Port);
                }
            }

            return nonDirectOnly ? Task.FromResult<ServerInformationComplete>(null) : GetInformationDirectly();
        }

        #region Kunos-specific
        private bool _fromKunosList;

        public bool FromKunosList {
            get => _fromKunosList;
            private set => Apply(value, ref _fromKunosList);
        }
        #endregion

        #region LAN-related
        private bool _fromLan;

        public bool FromLan {
            get => _fromLan;
            private set => Apply(value, ref _fromLan);
        }
        #endregion

        #region Minorating-related
        private bool _fromMinoratingList;

        public bool FromMinoratingList {
            get => _fromMinoratingList;
            private set => Apply(value, ref _fromMinoratingList);
        }
        #endregion

        #region Lists-related
        private bool _isFavourite;

        public bool IsFavourite {
            get => _isFavourite;
            set {
                if (Equals(value, _isFavourite)) return;
                // SetIsFavorited(value);

                if (value) {
                    FileBasedOnlineSources.AddToList(FileBasedOnlineSources.FavouritesKey, this);
                } else {
                    FileBasedOnlineSources.RemoveFromList(FileBasedOnlineSources.FavouritesKey, this);
                }
            }
        }

        /// <summary>
        /// For internal use.
        /// </summary>
        /// <param name="value">New value.</param>
        private void SetIsFavourited(bool value) {
            if (Equals(value, _isFavourite)) return;
            _isFavourite = value;
            OnPropertyChanged(nameof(IsFavourite));
        }

        private DelegateCommand _toggleFavoriteCommand;

        public DelegateCommand ToggleFavouriteCommand => _toggleFavoriteCommand ?? (_toggleFavoriteCommand = new DelegateCommand(() => {
            IsFavourite = !IsFavourite;
        }));

        private bool _isExcluded;

        public bool IsExcluded {
            get => _isExcluded;
            set {
                if (Equals(value, _isExcluded)) return;
                // SetIsHidden(value);

                if (value) {
                    FileBasedOnlineSources.AddToList(FileBasedOnlineSources.HiddenKey, this);
                } else {
                    FileBasedOnlineSources.RemoveFromList(FileBasedOnlineSources.HiddenKey, this);
                }
            }
        }

        /// <summary>
        /// For internal use.
        /// </summary>
        /// <param name="value">New value.</param>
        private void SetIsExcluded(bool value) {
            if (Equals(value, _isExcluded)) return;
            _isExcluded = value;
            OnPropertyChanged(nameof(IsExcluded));
        }

        private DelegateCommand _toggleHiddenCommand;

        public DelegateCommand ToggleHiddenCommand => _toggleHiddenCommand ?? (_toggleHiddenCommand = new DelegateCommand(() => {
            IsExcluded = !IsExcluded;
        }));

        private bool _wasUsedRecently;

        public bool WasUsedRecently {
            get => _wasUsedRecently;
            private set => Apply(value, ref _wasUsedRecently);
        }
        #endregion
    }
}
