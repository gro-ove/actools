using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Helpers.Api.Kunos;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Online {
    public class KunosOnlineSource : IOnlineListSource {
        public const string Key = @"kunos";
        public static readonly KunosOnlineSource Instance = new KunosOnlineSource();

        string IWithId.Id => Key;

        public string DisplayName => "Kunos";

        event EventHandler IOnlineSource.Obsolete {
            add { }
            remove { }
        }

        private class ProgressConverter : IProgress<int> {
            private readonly IProgress<AsyncProgressEntry> _target;

            public ProgressConverter([NotNull] IProgress<AsyncProgressEntry> target) {
                _target = target;
            }

            public void Report(int value) {
                _target.Report(value == 0 ? AsyncProgressEntry.Indetermitate :
                        AsyncProgressEntry.FromStringIndetermitate($"Fallback to server #{value + 1}"));
            }
        }

        public async Task<bool> LoadAsync(Action<IEnumerable<ServerInformation>> callback, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            if (SteamIdHelper.Instance.Value == null) {
                throw new Exception(ToolsStrings.Common_SteamIdIsMissing);
            }

            var data = await Task.Run(() => KunosApiProvider.TryToGetList(progress == null ? null : new ProgressConverter(progress)), cancellation);
            if (cancellation.IsCancellationRequested) return false;

            if (data == null) {
                throw new InformativeException(ToolsStrings.Online_CannotLoadData, ToolsStrings.Common_MakeSureInternetWorks);
            }

            callback(data);
            return true;
        }
    }

    public class MinoratingOnlineSource : IOnlineListSource {
        public const string Key = @"minorating";
        public static readonly MinoratingOnlineSource Instance = new MinoratingOnlineSource();

        string IWithId.Id => Key;

        public string DisplayName => "Minorating";

        event EventHandler IOnlineSource.Obsolete {
            add { }
            remove { }
        }

        public async Task<bool> LoadAsync(Action<IEnumerable<ServerInformation>> callback, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            var data = await Task.Run(() => KunosApiProvider.TryToGetMinoratingList(), cancellation);
            if (cancellation.IsCancellationRequested) return false;

            if (data == null) {
                throw new InformativeException(ToolsStrings.Online_CannotLoadData, ToolsStrings.Common_MakeSureInternetWorks);
            }

            callback(data);
            return true;
        }
    }

    public class LanOnlineSource : IOnlineBackgroundSource {
        public const string Key = @"lan";
        public static readonly LanOnlineSource Instance = new LanOnlineSource();

        string IWithId.Id => Key;

        public string DisplayName => "LAN";

        event EventHandler IOnlineSource.Obsolete {
            add { }
            remove { }
        }

        public async Task<bool> LoadAsync(Action<ServerInformation> callback, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            await KunosApiProvider.TryToGetLanListAsync(callback, progress, cancellation);
            return !cancellation.IsCancellationRequested;
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

        public IEnumerable<IOnlineSource> GetSources() {
            return _sources.Values;
        }

        public IOnlineSource GetSource(string key) {
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
                Logging.Debug("Sources directory missing");
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
                    Logging.Debug("Missing source re-created");
                    _missing.Remove(key);
                    _sources[key] = source;
                    source.CheckIfChanged();
                    changed = true;
                } else if (_sources.TryGetValue(key, out source)) {
                    source.CheckIfChanged();
                } else {
                    Logging.Debug("New source added");
                    source = new Source(filename, key);
                    _sources[key] = source;
                    changed = true;
                }
            }

            foreach (var source in _sources.Where(s => !files.Any(x => string.Equals(x, s.Value.Filename, StringComparison.OrdinalIgnoreCase))).ToList()) {
                Logging.Debug("Existing source missing");
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
                DisplayName = Path.GetFileNameWithoutExtension(filename);
            }

            public string Id { get; }

            public string DisplayName { get; }

            private DateTime? _lastDateTime;

            public event EventHandler Obsolete;

            public async Task<bool> LoadAsync(Action<IEnumerable<ServerInformation>> callback, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
                var fileInfo = new FileInfo(Filename);
                if (!fileInfo.Exists) {
                    _lastDateTime = default(DateTime);

                    // should throw an exception?
                    return true;
                }

                _lastDateTime = fileInfo.LastWriteTimeUtc;

                var lines = await FileUtils.ReadAllLinesAsync(Filename, cancellation);
                if (cancellation.IsCancellationRequested) return false;

                callback(lines.Select(x => x.Trim()).Where(x => x.Length > 0 && !x.StartsWith(@"#")).Distinct()
                              .Select(ServerInformation.FromAddress).NonNull());
                return true;
            }

            public void CheckIfChanged() {
                if (!_lastDateTime.HasValue) return;

                var fileInfo = new FileInfo(Filename);
                DateTime? value = fileInfo.Exists ? fileInfo.LastWriteTime : default(DateTime);

                if (value != _lastDateTime) {
                    Obsolete?.Invoke(this, EventArgs.Empty);
                    _lastDateTime = value;
                }
            }
        }
    }
}