using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Utils;
using AcTools.Render.Temporary;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5Specific.Textures {
    public class Kn5OverrideableTexturesProvider : Kn5TexturesProvider {
        [CanBeNull]
        private string _directory;

        private IDeviceContextHolder _holder;
        private FileSystemWatcher _watcher;

        public Kn5OverrideableTexturesProvider([NotNull] Kn5 kn5) : base(kn5) {}

        public void ClearOverridesDirectory() {
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
            }

            foreach (var texture in GetExistingTextures()) {
                // TODO
                ((RenderableTexture)texture).LoadOverride(null, null);
            }
        }

        public void SetOverridesDirectory([NotNull] IDeviceContextHolder holder, [NotNull] string directory) {
            ClearOverridesDirectory();

            _directory = directory;
            _watcher = new FileSystemWatcher(Path.GetDirectoryName(directory) ?? "") {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName
            };
            _watcher.Changed += Watcher_Changed;
            _watcher.Created += Watcher_Created;
            _watcher.Deleted += Watcher_Deleted;
            _watcher.Renamed += Watcher_Renamed;
            _watcher.EnableRaisingEvents = true;
            _holder = holder;

            UpdateOverridesAsync().Forget();
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
            if (local == string.Empty) {
                UpdateOverridesLater();
            } else if (local != null) {
                UpdateOverrideLater(e.FullPath);
            }
        }

        private void Watcher_Deleted(object sender, FileSystemEventArgs e) {
            if (!LiveReload || _directory == null) return;

            var local = FileUtils.TryToGetRelativePath(e.FullPath, _directory);
            if (local == string.Empty) {
                UpdateOverridesLater();
            } else if (local != null) {
                UpdateOverrideLater(e.FullPath);
            }
        }

        private void Watcher_Renamed(object sender, RenamedEventArgs e) {
            if (!LiveReload || _directory == null) return;

            var localOld = FileUtils.TryToGetRelativePath(e.OldFullPath, _directory);
            var localNew = FileUtils.TryToGetRelativePath(e.FullPath, _directory);

            if (localOld == string.Empty || localNew == string.Empty) {
                UpdateOverridesLater();
            } else {
                if (localNew != null) {
                    UpdateOverrideLater(e.FullPath);
                }
                if (localOld != null) {
                    UpdateOverrideLater(e.OldFullPath);
                }
            }
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

        private bool _magickOverride;

        public bool MagickOverride {
            get { return _magickOverride; }
            set {
                if (Equals(value, _magickOverride)) return;
                _magickOverride = value;
                UpdateOverridesLater();
            }
        }

        private static readonly string[] MagickExtensions = {
            ".bmp",
            ".gif",
            ".hdr",
            ".ico",
            ".jpeg",
            ".jpg",
            ".png",
            ".psb",
            ".psd",
            ".svg",
            ".tga",
            ".tif",
            ".tiff",
            ".xcf"
        };

        private async void UpdateOverrideLater([NotNull] string filename) {
            if (_directory == null) return;

            var local = FileUtils.TryToGetRelativePath(filename, _directory)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(local)) return;

            IRenderableTexture texture;
            Textures.TryGetValue(local, out texture);

            var i = 5;
            byte[] bytes = null;
            var magickMode = texture == null;
            if (MagickOverride && magickMode) {
                if (!ImageUtils.IsMagickSupported) return;

                var ext = MagickExtensions.FirstOrDefault(x => local.EndsWith(x, StringComparison.Ordinal));
                if (ext != null) {
                    local = local.ApartFromLast(ext);

                    if (!Textures.TryGetValue(local, out texture)) {
                        Textures.TryGetValue(local + @".dds", out texture);
                    }
                }
            }

            if (texture == null) return;

            while (i-- > 0) {
                try {
                    await Task.Delay(200);
                    bytes = await FileUtils.ReadAllBytesAsync(filename);

                    if (magickMode) {
                        var b = bytes;
                        bytes = await Task.Run(() => ImageUtils.LoadAsConventionalBuffer(b));
                    }

                    break;
                } catch (Exception e) {
                    Logging.Warning("UpdateOverrideLater(): " + e);
                }
            }

            // TODO
            await ((RenderableTexture)texture).LoadOverrideAsync(_holder.Device, bytes);
            _holder.RaiseUpdateRequired();
        }

        private bool _updating;

        private async void UpdateOverridesLater() {
            if (_updating) return;
            _updating = true;

            await Task.Delay(100);
            await UpdateOverridesAsync();

            _updating = false;
        }

        protected override IRenderableTexture CreateTexture(IDeviceContextHolder contextHolder, string key) {
            var result = new RenderableTexture(key) { Resource = null };

            byte[] data;
            if (_kn5.TexturesData.TryGetValue(key, out data)) {
                result.LoadAsync(contextHolder.Device, data).Forget();
            }

            LoadOverrideAsync(contextHolder, result, key).Forget();
            return result;
        }

        private async Task<bool> LoadOverrideAsync(IDeviceContextHolder contextHolder, RenderableTexture texture, string textureName) {
            var overrided = await GetOverridedDataAsync(textureName);
            if (overrided == null) return false;
            texture.LoadOverrideAsync(contextHolder.Device, overrided).Forget();
            return true;
        }

        [CanBeNull]
        private string GetOverridedFilename(string name) {
            return _directory == null ? null : Path.Combine(_directory, name);
        }

        [CanBeNull]
        private byte[] GetOverridedData(string name) {
            var filename = GetOverridedFilename(name);
            if (filename == null) return null;

            if (ImageUtils.IsMagickSupported && MagickOverride) {
                foreach (var ext in MagickExtensions.Where(x => !filename.EndsWith(x, StringComparison.OrdinalIgnoreCase))) {
                    var candidate = filename + ext;
                    byte[] bytes = null;
                    if (File.Exists(candidate)) {
                        bytes = File.ReadAllBytes(candidate);
                    } else {
                        candidate = filename.ApartFromLast(Path.GetExtension(filename)) + ext;
                        if (File.Exists(candidate)) {
                            bytes = File.ReadAllBytes(candidate);
                        }
                    }

                    if (bytes != null) {
                        return ImageUtils.LoadAsConventionalBuffer(bytes);
                    }
                }
            }

            return File.Exists(filename) ? File.ReadAllBytes(filename) : null;
        }

        [ItemCanBeNull]
        private async Task<byte[]> GetOverridedDataAsync(string name) {
            var filename = GetOverridedFilename(name);
            if (filename == null) return null;

            if (ImageUtils.IsMagickSupported && MagickOverride) {
                foreach (var ext in MagickExtensions.Where(x => !filename.EndsWith(x, StringComparison.OrdinalIgnoreCase))) {
                    var candidate = filename + ext;
                    byte[] bytes = null;
                    if (File.Exists(candidate)) {
                        bytes = await FileUtils.ReadAllBytesAsync(candidate);
                    } else {
                        candidate = filename.ApartFromLast(Path.GetExtension(filename)) + ext;
                        if (File.Exists(candidate)) {
                            bytes = await FileUtils.ReadAllBytesAsync(candidate);
                        }
                    }

                    if (bytes != null) {
                        return await Task.Run(() => ImageUtils.LoadAsConventionalBuffer(bytes));
                    }
                }
            }

            return File.Exists(filename) ? await FileUtils.ReadAllBytesAsync(filename) : null;
        }

        private Task UpdateOverridesAsync(string textureName = null) {
            if (_holder == null) return Task.Delay(0);

            if (OptionOverrideAsync) {
                return Task.WhenAll(Textures.Values
                                             .Where(x => textureName == null || string.Equals(x.Name, textureName, StringComparison.OrdinalIgnoreCase))
                                             .OfType<RenderableTexture>().Select(async texture => {
                                                 var overrided = await GetOverridedDataAsync(texture.Name);
                                                 if (overrided != null) {
                                                     await texture.LoadOverrideAsync(_holder.Device, overrided);
                                                     _holder.RaiseUpdateRequired();
                                                 } else {
                                                     texture.Override = null;
                                                 }
                                             }));
            }


            foreach (var texture in Textures.Values
                                             .Where(x => textureName == null || string.Equals(x.Name, textureName, StringComparison.OrdinalIgnoreCase))
                                             .OfType<RenderableTexture>()) {
                try {
                    var overrided = GetOverridedData(texture.Name);
                    if (overrided != null) {
                        texture.LoadOverride(_holder.Device, overrided);
                    } else {
                        texture.Override = null;
                    }
                } catch (Exception e) {
                    Logging.Warning("Can’t load override texture: " + e);
                    texture.Override = null;
                }
            }

            _holder.RaiseUpdateRequired();
            return Task.Delay(0);
        }

        public override void Dispose() {
            base.Dispose();
            ClearOverridesDirectory();
        }
    }
}
