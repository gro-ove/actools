using System;
using System.Collections.Generic;
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

        public string Label {
            get { return _label; }
            private set {
                if (value == _label) return;
                _label = value;
                OnPropertyChanged();
            }
        }

        public Color? Color {
            get { return _color; }
            private set {
                if (value.Equals(_color)) return;
                _color = value;
                OnPropertyChanged();
            }
        }

        public bool Hidden {
            get { return _hidden; }
            private set {
                if (value == _hidden) return;
                _hidden = value;
                OnPropertyChanged();
            }
        }

        public bool Excluded {
            get { return _excluded; }
            private set {
                if (value == _excluded) return;
                _excluded = value;
                OnPropertyChanged();
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

            Label = label;
            Color = color;
            Hidden = hidden;
            Excluded = excluded;
        }
    }

    public class FileBasedOnlineSources : AbstractFilesStorage {
        public const string RecentKey = @"recent";
        public const string FavoritesKey = @"favorites";

        public event EventHandler Update;

        public static IOnlineSource RecentInstance { get; private set; }

        public static IOnlineSource FavoritesInstance { get; private set; }

        private static FileBasedOnlineSources _instance;

        public static FileBasedOnlineSources Instance => _instance ?? (_instance = new FileBasedOnlineSources());

        private const string Extension = ".txt";

        private FileBasedOnlineSources() : base(FilesStorage.Instance.GetDirectory("Online Servers")) {}

        public void Initialize() {
            Watcher().Update += OnUpdate;
            Rescan();

            RecentInstance = GetSource(RecentKey);
            FavoritesInstance = GetSource(FavoritesKey);
        }

        public static void AddToList(string key, ServerEntry server) {
            Instance.GetInternalSource(key).Add(server);
        }

        public static void RemoveFromList(string key, ServerEntry server) {
            Instance.GetInternalSource(key).Remove(server);
        }

        public IEnumerable<string> GetSourceKeys(bool incudeAll = false) {
            return incudeAll ? _sources.Keys : _sources.Where(x => x.Value.Information?.Excluded != true).Select(x => x.Key);
        }

        public IEnumerable<IOnlineSource> GetSources() {
            return _sources.Values;
        }

        public IEnumerable<IOnlineSource> GetVisibleSources() {
            return _sources.Values.Where(x => x.Information?.Hidden != true);
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

            var filename = Path.Combine(RootDirectory, key + Extension);
            source = new Source(filename, key);
            _missing[key] = new WeakReference<Source>(source);
            return source;
        }

        private readonly Dictionary<string, Source> _sources = new Dictionary<string, Source>(2);
        private readonly Dictionary<string, WeakReference<Source>> _missing = new Dictionary<string, WeakReference<Source>>();

        private void Rescan() {
            if (!Directory.Exists(RootDirectory)) {
                foreach (var source in _sources) {
                    _missing[source.Key] = new WeakReference<Source>(source.Value);
                    source.Value.CheckIfChanged();
                }

                _sources.Clear();
                Update?.Invoke(this, EventArgs.Empty);
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
                Update?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnUpdate(object sender, EventArgs e) {
            Rescan();
        }

        private sealed class Source : IOnlineListSource {
            internal readonly string Filename;

            public Source(string filename, string local) {
                Id = local;
                Filename = filename;
                DisplayName = Path.GetFileNameWithoutExtension(filename) ?? @"?";
                Information = new OnlineSourceInformation();
                UpdateMetaInformation();
            }

            private void UpdateMetaInformation() {
                if (File.Exists(Filename)) {
                    using (var sr = new StreamReader(Filename, Encoding.UTF8)) {
                        AssignMetaInformation(sr);
                    }
                } else {
                    AssignMetaInformation(null);
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

            private void AssignMetaInformation([CanBeNull] StreamReader sr) {
                Information.Assign(sr == null ? null : ReadMetaInformation(sr));
            }

            private bool _justUpdated;

            public async Task<bool> LoadAsync(Action<IEnumerable<ServerInformation>> callback, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
                _justUpdated = true;

                var fileInfo = new FileInfo(Filename);
                if (!fileInfo.Exists) {
                    _lastDateTime = default(DateTime);
                    AssignMetaInformation(null);

                    // should throw an exception?
                    return true;
                }

                _lastDateTime = fileInfo.LastWriteTimeUtc;

                var result = new List<ServerInformation>(Math.Min((int)(fileInfo.Length / 20), 100000));
                using (var sr = new StreamReader(Filename, Encoding.UTF8)) {
                    AssignMetaInformation(sr);

                    string line;
                    while ((line = await sr.ReadLineAsync()) != null) {
                        if (cancellation.IsCancellationRequested) return false;

                        var entry = ParseLine(line);
                        if (entry != null && !result.Contains(entry, Comparer.ComparerInstance)) {
                            result.Add(entry);
                        }
                    }
                }

                callback(result);
                return true;
            }

            /// <summary>
            /// Check if file was changed, raise Obsolete event if was.
            /// </summary>
            /// <returns>True if FileBasedOnlineSource.Update event should be raised.</returns>
            public bool CheckIfChanged() {
                var fileInfo = new FileInfo(Filename);
                DateTime? value = fileInfo.Exists ? fileInfo.LastWriteTime : default(DateTime);

                if (value == _lastDateTime) {
                    return false;
                }

                _justUpdated = false;
                var hidden = Information?.Hidden == true;

                Obsolete?.Invoke(this, EventArgs.Empty);

                if (!_justUpdated) {
                    UpdateMetaInformation();
                }

                _lastDateTime = value;
                return hidden != (Information?.Hidden == true);
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

            public void Remove(ServerEntry entry) {
                if (File.Exists(Filename)) {
                    var lines = File.ReadAllLines(Filename).ToList();
                    for (var i = 0; i < lines.Count; i++) {
                        if (ParseLine(lines[i])?.Id == entry.Id) {
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
        }

        [CanBeNull]
        public OnlineSourceInformation GetInformation(string id) {
            return _sources.GetValueOrDefault(id)?.Information;
        }
    }
}