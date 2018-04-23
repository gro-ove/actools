using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private readonly Func<ILapTimesReader> _readerFn;

        private readonly Func<Task> _preparationFn;
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

        private ILapTimesReader _stayedReader;

        [NotNull]
        public IDisposable GetReader([CanBeNull] out ILapTimesReader reader) {
            var r = _stayedReader ?? _readerFn?.Invoke();
            reader = r;

            if (r?.CanStay == true) {
                _stayedReader = r;
                return ActionAsDisposable.Empty;
            }

            return r ?? ActionAsDisposable.Empty;
        }

        public List<LapTimesExtraTool> ExtraTools { get; } = new List<LapTimesExtraTool>();

        public LapTimesSource([NotNull] string id, string displayName, string description, string enabledKey, bool enabledByDefault,
                bool autoAddAllowed, [CanBeNull] Func<ILapTimesReader> readerFn, Func<Task> preparationFn) {
            _changeId = id.GetHashCode();

            _readerFn = readerFn;
            _preparationFn = preparationFn;

            Id = id;
            DisplayName = displayName;
            Description = description;
            AutoAddAllowed = autoAddAllowed;

            _storage = new LapTimesStorage(DisplayName, Id);

            _enabledKey = enabledKey;
            _autoAddKey = _enabledKey + @":autoAdd";
            var enabledByDefault1 = enabledByDefault;
            _isEnabled = ValuesStorage.Get(_enabledKey, enabledByDefault1);
            _autoAddEntries = ValuesStorage.Get(_autoAddKey, enabledByDefault1);
        }

        private string _detailsUrl;

        public string DetailsUrl {
            get => _detailsUrl;
            set => Apply(value, ref _detailsUrl);
        }

        public int? EntriesCount {
            get {
                if (!IsEnabled) return null;
                EnsureActualAsync(false).Forget();
                return _list?.Count;
            }
        }

        private bool _isLoading;

        public bool IsLoading {
            get => _isLoading;
            set => Apply(value, ref _isLoading);
        }

        public DateTime? LastModified => IsEnabled ? _storage.LastModified : null;

        private int _changeId, _listChangeId;
        private DateTime _lastLoaded;
        private IReadOnlyList<LapTimeEntry> _list;
        private bool _busy;

        public int ChangeId {
            get {
                if ((DateTime.Now - _lastLoaded).TotalSeconds > 10d) {
                    using (GetReader(out var reader)) {
                        if (reader != null && !_storage.IsActual(reader)) {
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

        private async Task EnsureActualAsync(bool force) {
            if (_busy) return;
            _busy = true;

            try {
                using (GetReader(out var reader)) {
                    if (reader != null && (force || !_storage.IsActual(reader))) {
                        IsLoading = true;
                        if (_preparationFn != null) {
                            await _preparationFn();
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
                var s = Stopwatch.StartNew();
                await EnsureActualAsync(_list != null).ConfigureAwait(false);
                Logging.Debug($"{Id}: {s.Elapsed.TotalMilliseconds:F1} ms");
            }

            return _list ?? new LapTimeEntry[0];
        }

        public async Task AddEntryAsync(LapTimeEntry entry) {
            await EnsureActualAsync(false);

            try {
                if (!_storage.IsBetter(entry)) {
                    Logging.Debug($"Better entry available ({Id})");
                    return;
                }

                Logging.Debug($"New entry ({Id}): {entry.LapTime}");

                SetDirty();
                _storage.Set(entry);

                using (GetReader(out var reader)) {
                    if (reader != null) {
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
            await EnsureActualAsync(false);
            if (!_storage.Remove(carId, trackId)) return false;

            SetDirty();

            using (GetReader(out var reader)) {
                if (reader != null) {
                    await Task.Run(() => reader.Remove(carId, trackId));
                    _storage.SyncLastModified(reader);
                }
            }

            OnPropertyChanged(nameof(LastModified));
            OnPropertyChanged(nameof(EntriesCount));
            return true;
        }

        public void ClearCache() {
            if (_readerFn == null) return;
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

            if (_readerFn == null) {
                foreach (var entry in entries) {
                    _storage.Set(entry);
                }

                _storage.SyncLastModified();
            } else {
                using (GetReader(out var reader)) {
                    if (reader != null) {
                        await Task.Run(() => reader.Export(entries));
                        _storage.SyncLastModified(reader);
                    }
                }
            }

            OnPropertyChanged(nameof(LastModified));
            OnPropertyChanged(nameof(EntriesCount));
        }

        private bool? _readOnly;

        public bool ReadOnly => _readOnly ?? (_readOnly = !CanExport()).Value;

        private ICommand _clearCacheCommand;

        public ICommand ClearCacheCommand => _clearCacheCommand ?? (_clearCacheCommand = _readerFn == null ? UnavailableCommand.Instance
                : new DelegateCommand(ClearCache));

        private bool CanExport() {
            using (GetReader(out var reader)) {
                return reader?.CanExport ?? true;
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

        public bool ReaderBased => _readerFn != null;
    }
}