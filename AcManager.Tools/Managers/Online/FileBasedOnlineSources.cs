using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api.Kunos;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Online {
    public class OnlineSourceInformation : NotifyPropertyChanged {
        private string _label;
        private Color? _color;
        private bool _hidden;
        private bool _excluded;

        public OnlineSourceInformation() {}

        public OnlineSourceInformation(string id) {
            switch (id) {
                case FileBasedOnlineSources.HiddenKey:
                    SetHiddenInner(true);
                    SetExcludedInner(true);
                    break;
            }
        }

        public string Label {
            get { return _label; }
            set { SetLabelInner(value); }
        }

        private void SetLabelInner(string value) {
            if (string.IsNullOrWhiteSpace(value)) value = null;
            if (Equals(value, _label)) return;
            _label = value;
            OnPropertyChanged(nameof(Label));
        }

        public Color? Color {
            get { return _color; }
            set { SetColorInner(value); }
        }

        private void SetColorInner(Color? value) {
            if (Equals(value, _color)) return;
            _color = value;
            OnPropertyChanged(nameof(Color));
        }

        public bool Hidden {
            get { return _hidden; }
            set {
                SetHiddenInner(value);
                FileBasedOnlineSources.Instance.RaiseUpdated();
            }
        }

        private void SetHiddenInner(bool value) {
            if (Equals(value, _hidden)) return;
            _hidden = value;
            OnPropertyChanged(nameof(Hidden));
        }

        public bool Excluded {
            get { return _excluded; }
            set { SetExcludedInner(value); }
        }

        private void SetExcludedInner(bool value) {
            if (Equals(value, _excluded)) return;
            _excluded = value;
            OnPropertyChanged(nameof(Excluded));
        }

        internal void Export(IList<string> list) {
            if (!string.IsNullOrWhiteSpace(Label)) {
                list.Add($@"# label: {Label}");
            }

            if (Color.HasValue) {
                list.Add($@"# color: {Color.Value.ToHexString()}");
            }

            if (Hidden || Excluded) {
                list.Add($@"# hidden: {(Hidden ? @"yes" : @"no")}");
                list.Add($@"# excluded: {(Excluded ? @"yes" : @"no")}");
            }
        }

        internal void Assign([CanBeNull] IEnumerable<KeyValuePair<string, string>> values) {
            string label = null;
            Color? color = null;
            var hidden = false;
            var excluded = false;

            if (values != null) {
                foreach (var pair in values) {
                    switch (pair.Key) {
                        case "label":
                            label = pair.Value;
                            break;

                        case "color":
                            color = pair.Value?.ToColor();
                            break;

                        case "hidden":
                            hidden = FlexibleParser.ParseBool(pair.Value, false);
                            break;

                        case "excluded":
                            excluded = FlexibleParser.ParseBool(pair.Value, false);
                            break;
                    }
                }
            }

            SetLabelInner(label);
            SetColorInner(color);
            SetHiddenInner(hidden);
            SetExcludedInner(excluded);
        }
    }

    public class FileBasedOnlineSources : AbstractFilesStorage {
        public static int OptionRecentSize = 100;

        public const string RecentKey = @"recent";
        public const string FavouritesKey = @"favourites";
        public const string HiddenKey = @"hidden";

        public event EventHandler Update;

        public static IOnlineSource RecentInstance { get; private set; }

        public static IOnlineSource FavouritesInstance { get; private set; }

        public static IOnlineSource HiddenInstance { get; private set; }

        private static FileBasedOnlineSources _instance;

        public static FileBasedOnlineSources Instance => _instance ?? (_instance = new FileBasedOnlineSources());
        
        private const string Extension = ".txt";

        private FileBasedOnlineSources() : base(FilesStorage.Instance.GetDirectory("Online Servers")) {}

        public void Initialize() {
            Watcher().Update += OnUpdate;
            Rescan();

            RecentInstance = GetSource(RecentKey);
            FavouritesInstance = GetSource(FavouritesKey);
            HiddenInstance = GetSource(HiddenKey);
        }

        public static void AddToList(string key, ServerEntry server) {
            Instance.GetInternalSource(key).Add(server);
        }

        public static void AddToList(string key, ServerInformation serverInformation) {
            Instance.GetInternalSource(key).Add(serverInformation);
        }

        public static void RemoveFromList(string key, ServerEntry server) {
            Instance.GetInternalSource(key).Remove(server);
        }

        public IEnumerable<string> GetSourceKeys(bool incudeAll = false) {
            return incudeAll ? _sources.Keys :
                    _sources.Where(x => x.Key != HiddenKey && x.Value.Information?.Excluded != true).Select(x => x.Key);
        }

        public IEnumerable<string> GetSourceKeys(ServerEntry entry) {
            return _sources.Where(x => x.Value.Contains(entry)).Select(x => x.Key);

        }

        public IEnumerable<IOnlineSource> GetSources() {
            return _sources.Values;
        }

        public IEnumerable<IOnlineSource> GetVisibleSources() {
            return _sources.Where(x => x.Key != HiddenKey && x.Value.Information?.Hidden != true).Select(x => x.Value);
        }

        public IEnumerable<IOnlineSource> GetBuiltInSources() {
            return new[] { FavouritesInstance, RecentInstance, HiddenInstance };
        }

        public IEnumerable<IOnlineSource> GetUserSources() {
            return _sources.Where(x => x.Key != HiddenKey && x.Key != FavouritesKey && x.Key != RecentKey).Select(x => x.Value);
        }

        [NotNull]
        public IOnlineSource GetSource([NotNull] string key) {
            return GetInternalSource(key);
        }
        
        [NotNull]
        private Source GetInternalSource([NotNull] string key) {
            Source source;
            if (_sources.TryGetValue(key, out source)) {
                return source;
            }

            WeakReference<Source> removed;
            if (_missing.TryGetValue(key, out removed) && removed.TryGetTarget(out source)) {
                return source;
            }

            var filename = Path.Combine(RootDirectory, key.ToTitle(CultureInfo.InvariantCulture) + Extension);
            source = new Source(filename, key);
            _missing[key] = new WeakReference<Source>(source);
            return source;
        }

        private readonly Dictionary<string, Source> _sources = new Dictionary<string, Source>(2);
        private readonly Dictionary<string, WeakReference<Source>> _missing = new Dictionary<string, WeakReference<Source>>();

        internal void RaiseUpdated() {
            Update?.Invoke(this, EventArgs.Empty);
        }

        private void Rescan() {
            if (!Directory.Exists(RootDirectory)) {
                foreach (var source in _sources) {
                    _missing[source.Key] = new WeakReference<Source>(source.Value);
                    source.Value.CheckIfChanged();
                }

                _sources.Clear();
                RaiseUpdated();
                return;
            }

            var changed = false;
            var files = Directory.GetFiles(RootDirectory, "*" + Extension, SearchOption.AllDirectories);
            foreach (var filename in files) {
                var key = FileUtils.GetRelativePath(filename, RootDirectory).ToLowerInvariant().ApartFromLast(Extension);

                Source source;
                WeakReference<Source> removed;

                if (_missing.TryGetValue(key, out removed) && removed.TryGetTarget(out source)) {
                    _missing.Remove(key);
                    _sources[key] = source;
                    source.CheckIfChanged();
                    changed = true;
                } else if (_sources.TryGetValue(key, out source)) {
                    changed |= source.CheckIfChanged();
                } else {
                    source = new Source(filename, key);
                    _sources[key] = source;
                    changed = true;
                }
            }

            foreach (var source in _sources.Where(s => !files.Any(x => string.Equals(x, s.Value.Filename, StringComparison.OrdinalIgnoreCase))).ToList()) {
                _sources.Remove(source.Key);
                _missing[source.Key] = new WeakReference<Source>(source.Value);
                source.Value.CheckIfChanged();
                changed = true;
            }
        
            if (changed) {
                RaiseUpdated();
            }
        }

        private void OnUpdate(object sender, EventArgs e) {
            Rescan();
        }

        private sealed class Source : IOnlineListSource {
            private const int AverageSymbolsPerServer = 20;
            private const int MaximumDefaultCapacity = 100000;

            internal readonly string Filename;

            [CanBeNull]
            private List<ServerInformation> _entries;

            public Source(string filename, string local) {
                Id = local;
                Filename = filename;
                DisplayName = Path.GetFileNameWithoutExtension(filename) ?? @"?";
                Information = new OnlineSourceInformation();
                Information.PropertyChanged += Information_PropertyChanged;
                Reload();
            }

            internal bool Contains(ServerEntry entry) {
                return _entries?.GetByIdOrDefault(entry.Id) != null;
            }

            private void Reload() {
                _dirty++;

                var fileInfo = new FileInfo(Filename);
                if (fileInfo.Exists) {
                    using (var sr = new StreamReader(Filename, Encoding.UTF8)) {
                        AssignMetaInformation(sr);
                    }

                    var list = OnlineManager.Instance.List;

                    var actual = new List<ServerInformation>(Math.Min((int)(fileInfo.Length / AverageSymbolsPerServer), MaximumDefaultCapacity));
                    using (var sr = new StreamReader(Filename, Encoding.UTF8)) {
                        AssignMetaInformation(sr);

                        string line;
                        while ((line = sr.ReadLine()) != null) {
                            var entry = ParseLine(line);
                            if (entry != null && !actual.Contains(entry, Comparer.ComparerInstance)) {
                                actual.Add(entry);
                            }
                        }
                    }

                    actual.Capacity = actual.Count;
                    if (Id == RecentKey) {
                        actual.Reverse();
                    }

                    if (_entries == null) {
                        foreach (var entry in actual) {
                            list.GetByIdOrDefault(entry.Id)?.SetReference(Id);
                        }
                    } else {
                        foreach (var removed in _entries.Where(x => !actual.Contains(x, Comparer.ComparerInstance))) {
                            list.GetByIdOrDefault(removed.Id)?.RemoveOrigin(Id);
                        }

                        foreach (var added in actual.Where(x => !_entries.Contains(x, Comparer.ComparerInstance))) {
                            list.GetByIdOrDefault(added.Id)?.SetReference(Id);
                        }
                    }

                    _entries = actual;
                } else {
                    AssignMetaInformation(null);
                    if (_entries != null) {
                        var list = OnlineManager.Instance.List;
                        foreach (var entry in _entries) {
                            list.GetByIdOrDefault(entry.Id)?.RemoveOrigin(Id);
                        }
                        _entries = null;
                    }
                }
            }

            public string Id { get; }

            public string DisplayName { get; }

            public OnlineSourceInformation Information { get; }

            private DateTime? _lastDateTime;

            public event EventHandler Obsolete;

            private class Comparer : IEqualityComparer<ServerInformation> {
                public static readonly Comparer ComparerInstance = new Comparer();

                public bool Equals(ServerInformation x, ServerInformation y) {
                    return x.Id == y.Id;
                }

                public int GetHashCode(ServerInformation obj) {
                    return obj.Id.GetHashCode();
                }
            }

            [CanBeNull]
            private static ServerInformation ParseLine([NotNull] string line) {
                line = line.Trim();
                return line.Length > 0 && !line.StartsWith(@"#") ? ServerInformation.FromDescription(line) : null;
            }

            private readonly Regex _metaInformation = new Regex(@"\s*#\s*([\w-]+)\s*(?:[=:]\s*(.+))?$", RegexOptions.Compiled);

            private IEnumerable<KeyValuePair<string, string>> ReadMetaInformation([NotNull] StreamReader sr) {
                try {
                    string line;
                    while ((line = sr.ReadLine()) != null) {
                        if (string.IsNullOrWhiteSpace(line)) continue;

                        var match = _metaInformation.Match(line);
                        if (!match.Success) break;

                        var key = match.Groups[1].Value.ToLowerInvariant();
                        var value = match.Groups[2].Success ? match.Groups[2].Value.TrimEnd() : null;
                        yield return new KeyValuePair<string, string>(key, value);
                    }
                } finally {
                    sr.BaseStream.Position = 0;
                    sr.DiscardBufferedData();
                }
            }

            private void UpdateMetaInformation() {
                var information = Information;

                try {
                    var list = new List<string>();
                    information?.Export(list);

                    if (File.Exists(Filename)) {
                        using (var sr = new StreamReader(Filename, Encoding.UTF8)) {
                            var skip = true;

                            string line;
                            while ((line = sr.ReadLine()) != null) {
                                if (skip && !_metaInformation.IsMatch(line)) {
                                    skip = false;
                                }

                                if (!skip) {
                                    list.Add(line);
                                }
                            }
                        }
                    }

                    File.WriteAllText(Filename, list.JoinToString('\n'));
                    _lastDateTime = new FileInfo(Filename).LastWriteTime;
                } catch(Exception e) {
                    NonfatalError.Notify("Can’t save list information", e);
                }
            }

            private int _dirty;

            private async void Information_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
                if (e.PropertyName == nameof(Information.Excluded)) {
                    foreach (var entry in OnlineManager.Instance.List) {
                        if (entry.ReferencedFrom(Id)) {
                            entry.UpdateExcluded(Id, Information?.Excluded == true);
                        }
                    }
                }

                var dirty = ++_dirty;
                await Task.Delay(1000);
                if (dirty == _dirty) {
                    UpdateMetaInformation();
                }
            }

            private void AssignMetaInformation([CanBeNull] StreamReader sr) {
                var information = Information;
                information.PropertyChanged -= Information_PropertyChanged;
                information.Assign(sr == null ? null : ReadMetaInformation(sr));
                information.PropertyChanged += Information_PropertyChanged;
            }

            public Task<bool> LoadAsync(ListAddCallback<ServerInformation> callback, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
                if (_entries != null) {
                    callback(_entries);
                } else {
                    callback(new ServerInformation[0]);
                }

                return Task.FromResult(true);
            }

            /// <summary>
            /// Check if file was changed, raise Obsolete event if was.
            /// </summary>
            /// <returns>True if FileBasedOnlineSource.Update event should be raised.</returns>
            public bool CheckIfChanged() {
                var fileInfo = new FileInfo(Filename);
                DateTime? value = fileInfo.Exists ? fileInfo.LastWriteTime : default(DateTime);

                if (value != _lastDateTime) {
                    _lastDateTime = value;

                    var hidden = Information?.Hidden == true;
                    Reload();
                    Obsolete?.Invoke(this, EventArgs.Empty);
                    return hidden != (Information?.Hidden == true);
                }

                return false;
            }

            public void Add(ServerInformation entry) {
                if (File.Exists(Filename)) {
                    var lines = File.ReadAllLines(Filename);
                    for (var i = 0; i < lines.Length; i++) {
                        if (ParseLine(lines[i])?.Id == entry.Id) {
                            lines[i] = entry.ToDescription();
                            goto Save;
                        }
                    }

                    var newLines = new string[lines.Length + 1];
                    lines.CopyTo(newLines, 0);
                    newLines[lines.Length] = entry.ToDescription();
                    lines = newLines;

                    Save:
                    File.WriteAllLines(Filename, lines);
                } else {
                    File.WriteAllText(Filename, entry.ToDescription());
                }

                CheckIfChanged();
            }

            public void Add(ServerEntry entry) {
                if (File.Exists(Filename)) {
                    var lines = File.ReadAllLines(Filename);
                    for (var i = 0; i < lines.Length; i++) {
                        if (ParseLine(lines[i])?.Id == entry.Id) {
                            lines[i] = entry.ToDescription();
                            goto Save;
                        }
                    }

                    var newLines = new string[lines.Length + 1];
                    lines.CopyTo(newLines, 0);
                    newLines[lines.Length] = entry.ToDescription();
                    lines = newLines;

                    Save:
                    File.WriteAllLines(Filename, lines);
                } else {
                    File.WriteAllText(Filename, entry.ToDescription());
                }

                CheckIfChanged();
            }

            private void Remove(string id) {
                if (File.Exists(Filename)) {
                    var lines = File.ReadAllLines(Filename).ToList();
                    for (var i = 0; i < lines.Count; i++) {
                        if (ParseLine(lines[i])?.Id == id) {
                            lines.RemoveAt(i);
                            goto Save;
                        }
                    }

                    return;

                    Save:
                    File.WriteAllLines(Filename, lines);
                    CheckIfChanged();
                }
            }

            public void Remove(ServerInformation entry) {
                Remove(entry.Id);
            }

            public void Remove(ServerEntry entry) {
                Remove(entry.Id);
            }
        }

        [CanBeNull]
        public OnlineSourceInformation GetInformation(string key) {
            return _sources.GetValueOrDefault(key)?.Information;
        }

        public bool IsSourceExcluded(string key) {
            return GetInformation(key)?.Excluded == true;
        }

        public static void RenameList(string key, string newName) {
            try {
                File.Move(Path.Combine(Instance.RootDirectory, key + Extension), Path.Combine(Instance.RootDirectory, newName + Extension));
                Instance.Rescan();
            } catch (Exception e) {
                NonfatalError.Notify("Can’t rename list", e);
            }
        }

        public static void RemoveList(string key) {
            try {
                FileUtils.Recycle(Path.Combine(Instance.RootDirectory, key + Extension));
            } catch (Exception e) {
                NonfatalError.Notify("Can’t remove list", e);
            }
        }

        public static void CreateList(string name) {
            try {
                File.WriteAllText(Path.Combine(Instance.RootDirectory, name + Extension), "");
            } catch (Exception e) {
                NonfatalError.Notify("Can’t create list", e);
            }
        }

        public static async Task AddRecent(ServerEntry serverEntry) {
            var source = Instance.GetInternalSource(RecentKey);

            IReadOnlyList<ServerInformation> entries = null;
            await source.LoadAsync(x => entries = x.ToIReadOnlyListIfItsNot(), null, default(CancellationToken));

            if (entries?.Count > OptionRecentSize) {
                source.Remove(entries[0]);
            }

            source.Add(serverEntry);
        }
    }
}