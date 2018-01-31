using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AcTools.LapTimes;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Profile {
    public sealed class LapTimesSource : Displayable, IWithId {
        [CanBeNull]
        private readonly Func<ILapTimesReader> _readerFunc;

        private readonly Func<Task> _preparationFunc;
        private LapTimesStorage _storage;

        private readonly string _enabledKey;
        private readonly string _autoAddKey;

        private bool _isEnabled;

        public bool IsEnabled {
            get => _isEnabled;
            set {
                if (Equals(value, _isEnabled)) return;
                _isEnabled = value;
                ValuesStorage.Set(_enabledKey, value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(EntriesCount));
                OnPropertyChanged(nameof(LastModified));

                LapTimesManager.Instance.RaiseEntriesChanged();
            }
        }

        public string Description { get; }

        public bool AutoAddAllowed { get; }

        private bool _autoAddEntries;

        public bool AutoAddEntries {
            get => _autoAddEntries && AutoAddAllowed;
            set {
                if (Equals(value, _autoAddEntries)) return;
                _autoAddEntries = value;
                ValuesStorage.Set(_autoAddKey, value);
                OnPropertyChanged();
            }
        }

        public List<LapTimesExtraTool> ExtraTools { get; } = new List<LapTimesExtraTool>();

        public LapTimesSource([NotNull] string id, string displayName, string description, string enabledKey, bool enabledByDefault,
                bool autoAddAllowed, [CanBeNull] Func<ILapTimesReader> readerFunc, Func<Task> preparationFunc) {
            _changeId = id.GetHashCode();

            _readerFunc = readerFunc;
            _preparationFunc = preparationFunc;

            Id = id;
            DisplayName = displayName;
            Description = description;
            AutoAddAllowed = autoAddAllowed;

            _storage = new LapTimesStorage(DisplayName, Id);

            _enabledKey = enabledKey;
            _autoAddKey = _enabledKey + ":autoAdd";
            var enabledByDefault1 = enabledByDefault;
            _isEnabled = ValuesStorage.Get(_enabledKey, enabledByDefault1);
            _autoAddEntries = ValuesStorage.Get(_autoAddKey, enabledByDefault1);
        }

        private string _detailsUrl;

        public string DetailsUrl {
            get => _detailsUrl;
            set {
                if (Equals(value, _detailsUrl)) return;
                _detailsUrl = value;
                OnPropertyChanged();
            }
        }

        public int? EntriesCount {
            get {
                if (!IsEnabled) return null;
                EnsureActualAsync().Forget();
                return _list?.Count;
            }
        }

        private bool _isLoading;

        public bool IsLoading {
            get => _isLoading;
            set {
                if (Equals(value, _isLoading)) return;
                _isLoading = value;
                OnPropertyChanged();
            }
        }

        public DateTime? LastModified => IsEnabled ? _storage.LastModified : null;

        private int _changeId, _listChangeId;
        private DateTime _lastLoaded;
        private IReadOnlyList<LapTimeEntry> _list;
        private bool _busy;

        public int ChangeId {
            get {
                if (_readerFunc != null && (DateTime.Now - _lastLoaded).TotalSeconds > 10d) {
                    using (var reader = _readerFunc()) {
                        if (!_storage.IsActual(reader)) {
                            _changeId++;
                        }
                    }
                }

                return _changeId;
            }
        }

        private void SetDirty() {
            unchecked {
                _changeId++;
            }

            _list = null;
        }

        public async Task EnsureActualAsync() {
            if (_busy) return;
            _busy = true;

            try {
                if (_readerFunc != null) {
                    using (var reader = _readerFunc()) {
                        if (!_storage.IsActual(reader)) {
                            IsLoading = true;
                            if (_preparationFunc != null) {
                                await _preparationFunc();
                            }

                            _listChangeId = _changeId;
                            _list = await _storage.UpdateCachedAsync(reader);
                            _lastLoaded = DateTime.Now;
                            OnPropertyChanged(nameof(EntriesCount));
                            OnPropertyChanged(nameof(LastModified));
                            IsLoading = false;
                            return;
                        }
                    }
                }

                _listChangeId = _changeId;
                _list = _storage.GetCached().ToList();
                _lastLoaded = DateTime.Now;
                OnPropertyChanged(nameof(EntriesCount));
            } finally {
                _busy = false;
            }
        }

        [ItemNotNull]
        public async Task<IReadOnlyList<LapTimeEntry>> GetEntriesAsync() {
            if (_listChangeId != ChangeId || _list == null) {
                await EnsureActualAsync().ConfigureAwait(false);
            }

            return _list ?? new LapTimeEntry[0];
        }

        public async Task AddEntryAsync(LapTimeEntry entry) {
            await EnsureActualAsync();

            try {
                if (!_storage.IsBetter(entry)) {
                    Logging.Debug($"Better entry available ({Id})");
                    return;
                }

                Logging.Debug($"New entry ({Id}): {entry.LapTime}");

                SetDirty();
                _storage.Set(entry);

                if (_readerFunc != null) {
                    using (var reader = _readerFunc()) {
                        reader.Export(new[] { entry });
                        _storage.SyncLastModified(reader);
                    }
                }

                OnPropertyChanged(nameof(LastModified));
                OnPropertyChanged(nameof(EntriesCount));
            } catch (Exception e) {
                Logging.Error(e);
            }
        }

        public async Task<bool> RemoveAsync(string carId, string trackId) {
            await EnsureActualAsync();
            if (!_storage.Remove(carId, trackId)) return false;

            SetDirty();

            if (_readerFunc != null) {
                using (var reader = _readerFunc()) {
                    await Task.Run(() => reader.Remove(carId, trackId));
                    _storage.SyncLastModified(reader);
                }
            }

            OnPropertyChanged(nameof(LastModified));
            OnPropertyChanged(nameof(EntriesCount));
            return true;
        }

        public void ClearCache() {
            if (_readerFunc == null) return;
            // if reader is null, storage is the only source of lap times — don’t clear it!

            var filename = _storage.Filename;
            _storage.Dispose();
            File.Delete(filename);
            _storage = new LapTimesStorage(DisplayName, Id);
            SetDirty();
        }

        public string Id { get; }

        public async Task AddEntriesAsync(IEnumerable<LapTimeEntry> entries) {
            SetDirty();

            if (_readerFunc == null) {
                foreach (var entry in entries) {
                    _storage.Set(entry);
                }

                _storage.SyncLastModified();
            } else {
                using (var reader = _readerFunc()) {
                    await Task.Run(() => reader.Export(entries));
                    _storage.SyncLastModified(reader);
                }
            }

            OnPropertyChanged(nameof(LastModified));
            OnPropertyChanged(nameof(EntriesCount));
        }

        private bool? _readOnly;

        public bool ReadOnly => _readOnly ?? (_readOnly = !CanExport()).Value;

        private ICommand _clearCacheCommand;

        public ICommand ClearCacheCommand => _clearCacheCommand ?? (_clearCacheCommand = _readerFunc == null ? UnavailableCommand.Instance
                : new DelegateCommand(ClearCache));

        private bool CanExport() {
            if (_readerFunc == null) {
                return true;
            }

            using (var reader = _readerFunc()) {
                return reader.CanExport;
            }
        }

        private ICommand _exportCommand;

        public ICommand ExportCommand => _exportCommand ?? (_exportCommand = ReadOnly ? UnavailableCommand.Instance : new AsyncCommand(async () => {
            try {
                using (var waiting = WaitingDialog.Create("Loading lap times…")) {
                    await LapTimesManager.Instance.UpdateAsync();
                    waiting.Report("Exporting…");
                    await AddEntriesAsync(LapTimesManager.Instance.Entries);
                }
            } catch (Exception e) {
                NonfatalError.Notify("Can’t export lap times", e);
            }
        }));

        public bool ReaderBased => _readerFunc != null;
    }
}