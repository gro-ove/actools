using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcTools.Render.Base;
using AcTools.Render.Base.Utils;
using AcTools.Render.Temporary;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5Specific.Textures {
    public class DirectoryTexturesProvider : TexturesProviderBase {
        [CanBeNull]
        private string _directory;

        private IDeviceContextHolder _holder;
        private FileSystemWatcher _watcher;

        public DirectoryTexturesProvider() {}

        public void ClearDirectory() {
            if (_watcher != null) {
                _watcher.EnableRaisingEvents = false;
                _watcher.Changed -= Watcher_Changed;
                _watcher.Created -= Watcher_Created;
                _watcher.Deleted -= Watcher_Deleted;
                _watcher.Renamed -= Watcher_Renamed;
                _watcher.Dispose();
                _directory = null;
                _watcher = null;
                _holder = null;
                Textures.DisposeEverything();
            }
        }

        public void SetDirectory([NotNull] IDeviceContextHolder holder, [NotNull] string directory) {
            ClearDirectory();

            _directory = directory;
            _watcher = new FileSystemWatcher(directory) {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName
            };
            _watcher.Changed += Watcher_Changed;
            _watcher.Created += Watcher_Created;
            _watcher.Deleted += Watcher_Deleted;
            _watcher.Renamed += Watcher_Renamed;
            _watcher.EnableRaisingEvents = true;
            _holder = holder;
        }

        private bool _liveReload = true;

        public bool LiveReload {
            get { return _liveReload; }
            set {
                if (Equals(value, _liveReload)) return;
                _liveReload = value;
                UpdateOverridesLater();
            }
        }

        private bool _updating;

        private async void UpdateOverridesLater() {
            if (_updating) return;
            _updating = true;

            await Task.Delay(100);
            await UpdateOverridesAsync();
            _updating = false;
        }

        private void Watcher_Changed(object sender, FileSystemEventArgs e) {
            if (!LiveReload || _directory == null) return;

            var local = FileUtils.TryToGetRelativePath(e.FullPath, _directory);
            if (!string.IsNullOrEmpty(local)) {
                UpdateOverrideLater(e.FullPath);
            }
        }

        private void Watcher_Created(object sender, FileSystemEventArgs e) {
            if (!LiveReload || _directory == null) return;

            var local = FileUtils.TryToGetRelativePath(e.FullPath, _directory);
            if (!string.IsNullOrEmpty(local)) {
                UpdateOverrideLater(e.FullPath);
            }
        }

        private void Watcher_Deleted(object sender, FileSystemEventArgs e) {
            if (!LiveReload || _directory == null) return;

            var local = FileUtils.TryToGetRelativePath(e.FullPath, _directory);
            if (!string.IsNullOrEmpty(local)) {
                UpdateOverrideLater(e.FullPath);
            }
        }

        private void Watcher_Renamed(object sender, RenamedEventArgs e) {
            if (!LiveReload || _directory == null) return;

            var localOld = FileUtils.TryToGetRelativePath(e.OldFullPath, _directory);
            var localNew = FileUtils.TryToGetRelativePath(e.FullPath, _directory);

            if (!string.IsNullOrEmpty(localOld)) {
                UpdateOverrideLater(e.OldFullPath);
            }

            if (!string.IsNullOrEmpty(localNew)) {
                UpdateOverrideLater(e.FullPath);
            }
        }

        private async void UpdateOverrideLater([NotNull] string filename) {
            if (_directory == null) return;

            var local = FileUtils.TryToGetRelativePath(filename, _directory)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(local)) return;

            IRenderableTexture texture;
            Textures.TryGetValue(local, out texture);

            var i = 5;
            byte[] bytes = null;

            if (texture == null) return;

            while (i-- > 0) {
                try {
                    await Task.Delay(200);
                    bytes = await FileUtils.ReadAllBytesAsync(filename);
                    break;
                } catch (Exception e) {
                    Logging.Warning("UpdateOverrideLater(): " + e);
                }
            }

            // TODO
            await ((RenderableTexture)texture).LoadOverrideAsync(_holder.Device, bytes);
            _holder.RaiseUpdateRequired();
        }

        private Task UpdateOverridesAsync(string textureName = null) {
            if (_holder == null || _directory == null) return Task.Delay(0);

            if (OptionOverrideAsync) {
                return Task.WhenAll(Textures.Values
                                             .Where(x => textureName == null || string.Equals(x.Name, textureName, StringComparison.OrdinalIgnoreCase))
                                             .OfType<RenderableTexture>().Select(async texture => {
                                                 if (texture.Name == null) return;
                                                 var filename = Path.Combine(_directory, texture.Name);
                                                 if (File.Exists(filename)) {
                                                     await texture.LoadAsync(_holder.Device, filename);
                                                     _holder.RaiseUpdateRequired();
                                                 } else {
                                                     texture.Resource = null;
                                                 }
                                             }));
            }


            foreach (var texture in Textures.Values
                                             .Where(x => textureName == null || string.Equals(x.Name, textureName, StringComparison.OrdinalIgnoreCase))
                                             .OfType<RenderableTexture>()) {
                try {
                    if (texture.Name == null) continue;
                    var filename = Path.Combine(_directory, texture.Name);
                    if (File.Exists(filename)) {
                        texture.Load(_holder.Device, filename);
                    } else {
                        texture.Resource = null;
                    }
                } catch (Exception e) {
                    Logging.Warning("Can’t load override texture: " + e);
                    texture.Override = null;
                }
            }

            _holder.RaiseUpdateRequired();
            return Task.Delay(0);
        }

        protected override IRenderableTexture CreateTexture(IDeviceContextHolder contextHolder, string key) {
            if (_directory == null) return null;
            var filename = Path.Combine(_directory, key);

            if (!File.Exists(filename)) return null;

            var result = new RenderableTexture { Resource = null };
            result.LoadAsync(contextHolder.Device, filename).Forget();
            return Textures[filename] = result;
        }

        public override void Dispose() {
            ClearDirectory();
        }
    }
}