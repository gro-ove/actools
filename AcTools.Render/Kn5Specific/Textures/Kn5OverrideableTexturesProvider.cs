using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcTools.DataFile;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Temporary;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5Specific.Textures {
    public class Kn5SkinnableTexturesProvider : Kn5OverrideableTexturesProvider {
        public Kn5SkinnableTexturesProvider([NotNull] Kn5 kn5, bool asyncLoading, bool asyncOverride) : base(kn5, asyncLoading, asyncOverride) {}

        private IniFile _skinIni;

        public class AssetPath {
            public readonly bool CrewSection;
            public readonly string KeyName;
            public readonly string Location;

            public AssetPath(bool crewSection, string keyName, string location) {
                Location = location;
                CrewSection = crewSection;
                KeyName = keyName;
            }
        }

        private AssetPath GetAssetPath(string textureName) {
            switch (textureName?.ToLowerInvariant()) {
                case "2016_suit_diff.dds":
                case "2016_suit_diff.jpg":
                case "2016_suit_nm.dds":
                    return new AssetPath(false, "SUIT", "driver_suit");
                case "helmet_1969.dds":
                case "helmet_1969.jpg":
                case "helmet_1975.dds":
                case "helmet_1975.jpg":
                case "helmet_1985.dds":
                case "helmet_1985.jpg":
                case "helmet_2012.dds":
                case "helmet_2012.jpg":
                case "helmet_type1.jpg":
                    return new AssetPath(false, "HELMET", "driver_helmet");
                case "2016_gloves_diff.dds":
                case "2016_gloves_diff.jpg":
                case "2016_gloves_nm.dds":
                case "detail_alpinestars_00.dds":
                case "detail_alpinestars_00_nm.dds":
                    return new AssetPath(false, "GLOVES", "driver_gloves");
                case "driver_gloves.dds":
                case "driver_gloves_nm.dds":
                case "driver_suit_nm.dds":
                case "driver_suit2.dds":
                case "driver_suit2.jpg":
                    return new AssetPath(true, "SUIT", "crew_suit");
                case "crew_helmet_color.dds":
                    return new AssetPath(true, "HELMET", "crew_helmet");
                case "brands_crew.dds":
                case "brands_crew.jpg":
                case "brands_crew_nm.dds":
                    return new AssetPath(true, "BRAND", "crew_brand");
                default:
                    return null;
            }
        }

        public override void ClearOverridesDirectory() {
            DisposeHelper.Dispose(ref _contentTexturesWatching);
            _skinIni = null;
            ContentTexturesDirectory = null;
            base.ClearOverridesDirectory();
        }

        [CanBeNull]
        protected string ContentTexturesDirectory { get; private set; }

        private IDisposable _contentTexturesWatching;

        private static string GetContentTexturesDirectory([NotNull] string skinDirectory) {
            return Path.Combine(Path.GetDirectoryName( // content folder
                    Path.GetDirectoryName( // cars folder
                            Path.GetDirectoryName( // car folder
                                    Path.GetDirectoryName(skinDirectory)))) ?? "", // skins folder
                    "texture");
        }

        public override void SetOverridesDirectory(IDeviceContextHolder holder, string directory) {
            ClearOverridesDirectory();

            ContentTexturesDirectory = GetContentTexturesDirectory(directory);
            _contentTexturesWatching = DirectoryWatcher.WatchDirectory(ContentTexturesDirectory, filename => {
                if (CurrentDirectory != null) {
                    UpdateOverrideLater(Path.Combine(CurrentDirectory, "skin.ini"));
                }
            });

            SetOverridesDirectoryInner(holder, directory);
        }

        public override void Dispose() {
            base.Dispose();
            DisposeHelper.Dispose(ref _contentTexturesWatching);
        }

        protected override Task UpdateOverridesAsync(string textureName = null) {
            if (textureName == null) {
                _skinIni = null;
            }

            return base.UpdateOverridesAsync(textureName);
        }

        protected override async Task UpdateTexture(string localName) {
            if (localName == "skin.ini") {
                await Task.Delay(200);

                _skinIni = null;
                foreach (var texture in Textures.Values) {
                    if (GetAssetPath(texture.Name) != null) {
                        await UpdateTextureInner(texture.Name, false);
                    }
                }
            } else {
                await base.UpdateTexture(localName);
            }
        }

        protected override string GetOverridedFilename(string name) {
            var path = GetAssetPath(name);
            if (path != null && CurrentDirectory != null && ContentTexturesDirectory != null) {
                if (_skinIni == null) {
                    _skinIni = new IniFile(Path.Combine(CurrentDirectory, "skin.ini"));
                }

                if (!_skinIni.IsEmptyOrDamaged()) {
                    var local = _skinIni[path.CrewSection ? "CREW" : _skinIni.Keys.FirstOrDefault(x => x != "CREW") ?? "-"].GetNonEmpty(path.KeyName);
                    if (local != null) {
                        if (local.StartsWith("/") || local.StartsWith("\\")) {
                            local = local.Substring(1);
                        }

                        var defaultTexture = Path.Combine(ContentTexturesDirectory, path.Location, local, name);
                        if (File.Exists(defaultTexture)) {
                            return defaultTexture;
                        }
                    }
                }
            }

            return base.GetOverridedFilename(name);
        }
    }

    public class Kn5OverrideableTexturesProvider : Kn5TexturesProvider {
        private readonly bool _asyncOverride;

        [CanBeNull]
        private IDeviceContextHolder _holder;
        private IDisposable _watching;

        [CanBeNull]
        protected string CurrentDirectory { get; private set; }

        public Kn5OverrideableTexturesProvider([NotNull] Kn5 kn5, bool asyncLoading, bool asyncOverride) : base(kn5, asyncLoading) {
            _asyncOverride = asyncOverride;
        }

        public virtual void ClearOverridesDirectory() {
            DisposeHelper.Dispose(ref _watching);
            foreach (var texture in GetExistingTextures()) {
                // TODO
                ((RenderableTexture)texture).LoadOverride(_holder, null);
            }

            _holder = null;
            CurrentDirectory = null;
        }

        protected void SetOverridesDirectoryInner([NotNull] IDeviceContextHolder holder, [NotNull] string directory) {
            _holder = holder;
            CurrentDirectory = directory;
            _watching = DirectoryWatcher.WatchDirectory(directory, filename => {
                if (filename == null) {
                    UpdateOverridesLater();
                } else {
                    UpdateOverrideLater(filename);
                }
            });

            UpdateOverridesAsync().Forget();
        }

        public virtual void SetOverridesDirectory([NotNull] IDeviceContextHolder holder, [NotNull] string directory) {
            ClearOverridesDirectory();
            SetOverridesDirectoryInner(holder, directory);
        }

        private bool _liveReload = true;

        public bool LiveReload {
            get => _liveReload;
            set {
                if (Equals(value, _liveReload)) return;
                _liveReload = value;
                UpdateOverridesLater();
            }
        }

        private bool _magickOverride;

        public bool MagickOverride {
            get => _magickOverride;
            set {
                if (Equals(value, _magickOverride)) return;
                _magickOverride = value;
                UpdateOverridesLater();
            }
        }

        private static readonly string[] MagickExtensions = {
            ".bmp",
            ".exr",
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

        private bool TryGetTexture(string name, out IRenderableTexture result) {
            foreach (var texture in Textures.Values) {
                if (string.Equals(texture.Name, name, StringComparison.OrdinalIgnoreCase)) {
                    result = texture;
                    return true;
                }
            }

            result = null;
            return false;
        }

        private readonly List<string> _updateInProcess = new List<string>(10);

        [ItemCanBeNull]
        protected async Task<byte[]> TryLoadBytes(string filename, bool magickMode, int attempts, bool initialDelay) {
            byte[] bytes = null;
            var second = false;
            while (attempts-- > 0) {
                try {
                    if (initialDelay || second) {
                        await Task.Delay(200).ConfigureAwait(false);
                    }

                    second = true;
                    bytes = await FileUtils.ReadAllBytesAsync(filename).ConfigureAwait(false);

                    if (magickMode) {
                        var b = bytes;
                        // bytes = await Task.Run(() => ImageUtils.LoadAsConventionalBuffer(b));
                        bytes = ImageUtils.LoadAsConventionalBuffer(b);
                    }

                    break;
                } catch (FileNotFoundException) {
                    return null;
                }catch (Exception e) {
                    AcToolsLogging.Write(e);
                    Logging.Warning("TryLoadBytes(): " + e.Message);
                }
            }

            return bytes;
        }

        protected async Task UpdateTextureInner(string localName, bool initialDelay) {
            IRenderableTexture texture;
            TryGetTexture(localName, out texture);

            var magickMode = texture == null;
            if (MagickOverride && magickMode) {
                if (!ImageUtils.IsMagickSupported) return;

                AcToolsLogging.Write($"Trying to update {localName}…");

                var ext = MagickExtensions.FirstOrDefault(x => localName.EndsWith(x, StringComparison.Ordinal));
                if (ext != null) {
                    var candidate = localName.ApartFromLast(ext);
                    if (!TryGetTexture(candidate, out texture)) {
                        TryGetTexture(candidate + @".dds", out texture);
                    }
                }
            }

            if (texture == null) return;

            var filename = GetOverridedFilename(localName);
            var bytes = await TryLoadBytes(filename, magickMode, 5, initialDelay);

            _updateInProcess.Remove(localName);

            // TODO
            // await ((RenderableTexture)texture).LoadOverrideAsync(_holder.Device, bytes);
            if (_holder == null) return;
            ((RenderableTexture)texture).LoadOverride(_holder, bytes);
            _holder.RaiseUpdateRequired();
        }

        protected virtual Task UpdateTexture(string localName) {
            return UpdateTextureInner(localName, true);
        }

        protected async void UpdateOverrideLater([NotNull] string filename) {
            if (CurrentDirectory == null) return;

            var local = FileUtils.TryToGetRelativePath(filename, CurrentDirectory)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(local)) return;

            if (_updateInProcess.Contains(local)) return;
            _updateInProcess.Add(local);

            try {
                await UpdateTexture(local);
            } catch (Exception e) {
                AcToolsLogging.Write(e);
            } finally {
                _updateInProcess.Remove(local);
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

        protected override IRenderableTexture CreateTexture(IDeviceContextHolder contextHolder, string key) {
            var result = new RenderableTexture(key) { Resource = null };

            byte[] data;
            if (Kn5.TexturesData.TryGetValue(key, out data)) {
                result.Exists = true;
                if (AsyncLoading && data.Length > 100000) {
                    result.LoadAsync(contextHolder, data).Forget();
                } else {
                    result.Load(contextHolder, data);
                }
            }

            if (CurrentDirectory != null) {
                if (_asyncOverride) {
                    LoadOverrideAsync(contextHolder, result, key).Forget();
                } else {
                    LoadOverride(contextHolder, result, key);
                }
            }

            return result;
        }

        private bool LoadOverride(IDeviceContextHolder contextHolder, RenderableTexture texture, string textureName) {
            var overrided = GetOverridedData(textureName);
            if (overrided == null) return false;
            texture.LoadOverride(contextHolder, overrided);
            return true;
        }

        private async Task<bool> LoadOverrideAsync(IDeviceContextHolder contextHolder, RenderableTexture texture, string textureName) {
            var overrided = await GetOverridedDataAsync(textureName);
            if (overrided == null) return false;
            texture.LoadOverrideAsync(contextHolder, overrided).Forget();
            return true;
        }

        [CanBeNull]
        protected byte[] LoadTexture([NotNull] string filename) {
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
        protected async Task<byte[]> LoadTextureAsync([NotNull] string filename) {
            if (ImageUtils.IsMagickSupported && MagickOverride) {
                foreach (var ext in MagickExtensions.Where(x => !filename.EndsWith(x, StringComparison.OrdinalIgnoreCase))) {
                    var candidate = filename + ext;
                    byte[] bytes = null;
                    if (File.Exists(candidate)) {
                        bytes = await FileUtils.ReadAllBytesAsync(candidate).ConfigureAwait(false);
                    } else {
                        candidate = filename.ApartFromLast(Path.GetExtension(filename)) + ext;
                        if (File.Exists(candidate)) {
                            bytes = await FileUtils.ReadAllBytesAsync(candidate).ConfigureAwait(false);
                        }
                    }

                    if (bytes != null) {
                        return await Task.Run(() => ImageUtils.LoadAsConventionalBuffer(bytes)).ConfigureAwait(false);
                    }
                }
            }

            return File.Exists(filename) ? await FileUtils.ReadAllBytesAsync(filename).ConfigureAwait(false) : null;
        }

        [CanBeNull]
        protected virtual string GetOverridedFilename(string name) {
            return CurrentDirectory == null ? null : Path.Combine(CurrentDirectory, name);
        }

        [CanBeNull]
        protected virtual byte[] GetOverridedData(string name) {
            var filename = GetOverridedFilename(name);
            return filename == null ? null : LoadTexture(filename);
        }

        [ItemCanBeNull]
        protected virtual async Task<byte[]> GetOverridedDataAsync(string name) {
            var filename = GetOverridedFilename(name);
            return filename == null ? null : await LoadTextureAsync(filename).ConfigureAwait(false);
        }

        protected virtual Task UpdateOverridesAsync(string textureName = null) {
            if (_holder == null) return Task.Delay(0);

            if (_asyncOverride) {
                return Task.WhenAll(Textures.Values
                                             .Where(x => textureName == null || string.Equals(x.Name, textureName, StringComparison.OrdinalIgnoreCase))
                                             .OfType<RenderableTexture>().Select(async texture => {
                                                 var overrided = await GetOverridedDataAsync(texture.Name);
                                                 if (_holder == null) return;

                                                 if (overrided != null) {
                                                     await texture.LoadOverrideAsync(_holder, overrided);
                                                     if (_holder == null) return;

                                                     _holder.RaiseUpdateRequired();
                                                 } else {
                                                     texture.Override = null;
                                                 }
                                             }));
            }

            var holder = _holder;
            if (holder != null) {
                foreach (var texture in Textures.Values
                                                .Where(x => textureName == null || string.Equals(x.Name, textureName, StringComparison.OrdinalIgnoreCase))
                                                .OfType<RenderableTexture>()) {
                    try {
                        var overrided = GetOverridedData(texture.Name);
                        if (overrided != null) {
                            texture.LoadOverride(holder, overrided);
                        } else {
                            texture.Override = null;
                        }
                    } catch (Exception e) {
                        Logging.Warning("Can’t load override texture: " + e.Message);
                        texture.Override = null;
                    }
                }

                holder.RaiseUpdateRequired();
            }

            return Task.Delay(0);
        }

        public override void Dispose() {
            base.Dispose();
            ClearOverridesDirectory();
        }
    }
}
