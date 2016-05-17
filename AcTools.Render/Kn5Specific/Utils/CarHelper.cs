using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcTools.DataFile;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Render.Temporary;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Kn5Specific.Utils {
    public class CarHelper : IOverridedTextureProvider, IDisposable {
        public Vector3 GetWheelShadowSize() {
            return new Vector3(0.3f, 1.0f, 0.3f);
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

        public readonly Kn5 MainKn5;
        public readonly string RootDirectory;
        public readonly string SkinsDirectory;
        public readonly string MainKn5Filename;
        public readonly DataWrapper Data;

        public List<string> Skins { get; private set; }

        public CarHelper(Kn5 kn5, string rootDirectory = null) {
            MainKn5 = kn5;

            RootDirectory = rootDirectory ?? Path.GetDirectoryName(kn5.OriginalFilename);
            SkinsDirectory = FileUtils.GetCarSkinsDirectory(RootDirectory);
            Data = DataWrapper.FromFile(RootDirectory);

            MainKn5Filename = FileUtils.GetMainCarFilename(RootDirectory, Data);
            ReloadSkins();
        }

        public event EventHandler SkinTextureUpdated;

        private FileSystemWatcher _watcher;
        private DeviceContextHolder _holder;

        public void SetupWatching(DeviceContextHolder holder) {
            if (!Directory.Exists(SkinsDirectory)) return;

            _watcher = new FileSystemWatcher(SkinsDirectory) {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.CreationTime
                        | NotifyFilters.DirectoryName
            };
            _watcher.Changed += Watcher_Changed;
            _watcher.Created += Watcher_Created;
            _watcher.Deleted += Watcher_Deleted;
            _watcher.Renamed += Watcher_Renamed;
            _watcher.EnableRaisingEvents = true;

            _holder = holder;
        }

        private async void UpdateOverrideLater(string filename) {
            var splitted = FileUtils.GetRelativePath(filename, SkinsDirectory).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (splitted.Length != 2) return;

            var skinId = splitted[0];
            var textureName = splitted[1];
            if (!string.Equals(CurrentSkin, skinId, StringComparison.OrdinalIgnoreCase)) return;

            var texturesProvider = _holder.Get<TexturesProvider>();
            var texture = texturesProvider.GetFor(MainKn5).OfType<RenderableTexture>().FirstOrDefault(x =>
                    string.Equals(x.Name, textureName, StringComparison.OrdinalIgnoreCase));

            var i = 5;
            byte[] bytes = null;
            var magickMode = texture == null;
            if (magickMode) {
                if (!ImageUtils.IsMagickSupported) return;
                Logging.Write("UpdateOverrideLater(): " + filename);

                var ext = MagickExtensions.FirstOrDefault(x => textureName.EndsWith(x, StringComparison.OrdinalIgnoreCase));
                if (ext != null) {
                    Logging.Write("UpdateOverrideLater(): ext: " + ext);
                    textureName = textureName.ApartFromLast(ext);
                    texture = texturesProvider.GetFor(MainKn5).OfType<RenderableTexture>().FirstOrDefault(x =>
                            string.Equals(x.Name, textureName, StringComparison.OrdinalIgnoreCase)) ??
                            texturesProvider.GetFor(MainKn5).OfType<RenderableTexture>().FirstOrDefault(x =>
                                    x.Name?.StartsWith(textureName, StringComparison.OrdinalIgnoreCase) == true &&
                                            x.Name.ElementAtOrDefault(textureName.Length) == '.');
                }
            }

            if (texture == null) return;

            while (i-- > 0) {
                try {
                    await Task.Delay(200);
                    bytes = await FileUtils.ReadAllBytesAsync(filename);

                    Logging.Write("UpdateOverrideLater(): loaded " + bytes?.Length + " bytes");

                    if (magickMode) {
                        var b = bytes;
                        bytes = await Task.Run(() => ImageUtils.LoadAsConventionalBuffer(b));
                        Logging.Write("UpdateOverrideLater(): converted " + bytes?.Length + " bytes");
                    }
                } catch (Exception e) {
                    Logging.Warning("UpdateOverrideLater(): " + e);
                }
            }

            await texture.LoadOverrideAsync(bytes, _holder.Device);
            SkinTextureUpdated?.Invoke(this, EventArgs.Empty);
        }

        private bool _reloading;

        private async void ReloadSkinsLater(string selectSkin = null) {
            if (_reloading) return;
            _reloading = true;

            await Task.Delay(100);
            ReloadSkins(selectSkin);

            var texturesProvider = _holder.Get<TexturesProvider>();
            await texturesProvider.UpdateOverridesForAsync(MainKn5, _holder);
            SkinTextureUpdated?.Invoke(this, EventArgs.Empty);

            _reloading = false;
        }

        private void Watcher_Changed(object sender, FileSystemEventArgs e) {
            if (!LiveReload) return;
            UpdateOverrideLater(e.FullPath);
        }

        private void Watcher_Created(object sender, FileSystemEventArgs e) {
            if (!LiveReload) return;

            var splitted = FileUtils.GetRelativePath(e.FullPath, SkinsDirectory).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (splitted.Length == 1) {
                ReloadSkinsLater();
            } else if (splitted.Length == 2) {
                UpdateOverrideLater(e.FullPath);
            }
        }

        private void Watcher_Deleted(object sender, FileSystemEventArgs e) {
            if (!LiveReload) return;

            var splitted = FileUtils.GetRelativePath(e.FullPath, SkinsDirectory).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (splitted.Length == 1) {
                ReloadSkinsLater();
            } else if (splitted.Length == 2) {
                UpdateOverrideLater(e.FullPath);
            }
        }

        private void Watcher_Renamed(object sender, RenamedEventArgs e) {
            if (!LiveReload) return;

            var splittedOld = FileUtils.GetRelativePath(e.OldFullPath, SkinsDirectory).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var splittedNew = FileUtils.GetRelativePath(e.FullPath, SkinsDirectory).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (splittedOld.Length == 1 && splittedNew.Length == 1) {
                ReloadSkinsLater(splittedNew[0]);
            } else if (splittedOld.Length == 2 && splittedNew.Length == 2) {
                UpdateOverrideLater(e.OldFullPath);
                UpdateOverrideLater(e.FullPath);
            } else if (splittedOld.Length == 1 && splittedNew.Length == 2) {
                ReloadSkinsLater();
                UpdateOverrideLater(e.FullPath);
            }
        }

        private bool _liveReload = true;

        public bool LiveReload {
            get { return _liveReload; }
            set {
                if (Equals(value, _liveReload)) return;
                _liveReload = value;
                ReloadSkinsLater();
            }
        }

        public string CurrentSkin { get; private set; }

        private void ReloadSkins(string selectSkin = null) {
            var selectedIndex = Skins?.IndexOf(CurrentSkin);

            try {
                Skins = Directory.GetDirectories(SkinsDirectory).Select(x => Path.GetFileName(x)?.ToLower()).ToList();
            } catch (Exception) {
                Skins = null;
            }

            CurrentSkin = Skins == null ? null :
                    Skins.FirstOrDefault(x => string.Equals(x, selectSkin ?? CurrentSkin, StringComparison.OrdinalIgnoreCase))
                            ?? Skins.ElementAtOrDefault(selectedIndex ?? -1)
                                    ?? Skins.FirstOrDefault();
        }

        public void SelectNextSkin(DeviceContextHolder contextHolder) {
            if (Skins.Any() != true) return;

            var index = Skins.IndexOf(CurrentSkin);
            SelectSkin(index < 0 || index >= Skins.Count - 1 ? Skins[0] : Skins[index + 1], contextHolder);
        }

        public void SelectPreviousSkin(DeviceContextHolder contextHolder) {
            if (Skins.Any() != true) return;

            var index = Skins.IndexOf(CurrentSkin);
            SelectSkin(index <= 0 ? Skins[Skins.Count - 1] : Skins[index - 1], contextHolder);
        }

        public void SelectSkin([CanBeNull] string skinId, DeviceContextHolder contextHolder) {
            CurrentSkin = skinId?.ToLower();
            var texturesProvider = contextHolder.Get<TexturesProvider>();
            texturesProvider.UpdateOverridesForAsync(MainKn5, contextHolder);
        }

        private string GetOverridedFilename(string name) {
            return CurrentSkin == null ? null : Path.Combine(SkinsDirectory, CurrentSkin, name);
        }

        public byte[] GetOverridedData(string name) {
            Logging.Write("GetOverridedData(): " + name);
            var filename = GetOverridedFilename(name);
            if (filename == null) return null;

            if (ImageUtils.IsMagickSupported && LiveReload) {
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
                        Logging.Write("  candidate: " + candidate);
                        Logging.Write("  found: " + bytes.Length + " bytes");
                        return ImageUtils.LoadAsConventionalBuffer(bytes);
                    }
                }
            }

            return File.Exists(filename) ? File.ReadAllBytes(filename) : null;
        }

        async Task<byte[]> IOverridedTextureProvider.GetOverridedDataAsync(string name) {
            var filename = GetOverridedFilename(name);
            if (filename == null) return null;

            Logging.Write("GetOverridedDataAsync(): " + name);
            if (ImageUtils.IsMagickSupported && LiveReload) {
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
                        Logging.Write("  candidate: " + candidate);
                        Logging.Write("  found: " + bytes.Length + " bytes");
                        return await Task.Run(() => ImageUtils.LoadAsConventionalBuffer(bytes));
                    }
                }
            }

            return File.Exists(filename) ? await FileUtils.ReadAllBytesAsync(filename) : null;
        }

        private IRenderableObject LoadWheelAmbientShadow(Kn5RenderableList main, string nodeName, string textureName) {
            var node = main.GetDummyByName(nodeName);
            if (node == null) return null;

            var wheel = node.Matrix.GetTranslationVector();
            wheel.Y = _shadowsHeight;

            var filename = Path.Combine(RootDirectory, textureName);
            return new AmbientShadow(filename, Matrix.Scaling(GetWheelShadowSize()) * Matrix.RotationY(MathF.PI) *
                    Matrix.Translation(wheel));
        }

        private Vector3 _ambientShadowSize;

        public Vector3 AmbientShadowSize {
            get { return _ambientShadowSize; }
            set {
                if (Equals(value, _ambientShadowSize)) return;
                _ambientShadowSize = value;

                if (AmbientShadowNode != null) {
                    AmbientShadowNode.Transform = Matrix.Scaling(AmbientShadowSize) * Matrix.RotationY(MathF.PI) *
                            Matrix.Translation(0f, _shadowsHeight, 0f);
                }
            }
        }

        public void FitAmbientShadowSize(Kn5RenderableList node) {
            if (!node.BoundingBox.HasValue) return;
            var size = node.BoundingBox.Value;
            AmbientShadowSize = new Vector3(Math.Max(-size.Minimum.X, size.Maximum.X) * 1.1f, 1.0f, Math.Max(-size.Minimum.Z, size.Maximum.Z) * 1.1f);
        }

        public void ResetAmbientShadowSize() {
            var iniFile = Data.GetIniFile("ambient_shadows.ini");
            AmbientShadowSize = new Vector3(
                    (float)iniFile["SETTINGS"].GetDouble("WIDTH", 1d), 1.0f,
                    (float)iniFile["SETTINGS"].GetDouble("LENGTH", 1d));
        }

        public AmbientShadow AmbientShadowNode { get; private set; }

        private float _shadowsHeight;

        private IRenderableObject LoadBodyAmbientShadow() {
            var filename = Path.Combine(RootDirectory, "body_shadow.png");
            AmbientShadowNode = new AmbientShadow(filename, Matrix.Identity);
            ResetAmbientShadowSize();
            return AmbientShadowNode;
        }

        public IEnumerable<IRenderableObject> LoadAmbientShadows(Kn5RenderableList node, float shadowsHeight = 0.01f) {
            _shadowsHeight = 0.01f;
            return Data.IsEmpty ? new IRenderableObject[0] : new[] {
                LoadBodyAmbientShadow(),
                LoadWheelAmbientShadow(node, "WHEEL_LF", "tyre_0_shadow.png"),
                LoadWheelAmbientShadow(node, "WHEEL_RF", "tyre_1_shadow.png"),
                LoadWheelAmbientShadow(node, "WHEEL_LR", "tyre_2_shadow.png"),
                LoadWheelAmbientShadow(node, "WHEEL_RR", "tyre_3_shadow.png")
            }.Where(x => x != null);
        }

        public IEnumerable<T> LoadLights<T>(Kn5RenderableList node) where T : CarLight, new() {
            if (Data.IsEmpty) return new T[0];

            var lightsIni = Data.GetIniFile("lights.ini");
            return lightsIni.GetSections("LIGHT").Select(x => {
                var t = new T();
                t.Initialize(CarLightType.Headlight, node, x);
                return t;
            }).Union(lightsIni.GetSections("BRAKE").Select(x => {
                var t = new T();
                t.Initialize(CarLightType.Brake, node, x);
                return t;
            }));
        }

        public IEnumerable<CarLight> LoadLights(Kn5RenderableList node) {
            if (Data.IsEmpty) return new CarLight[0];

            var lightsIni = Data.GetIniFile("lights.ini");
            return lightsIni.GetSections("LIGHT").Select(x => {
                var t = new CarLight();
                t.Initialize(CarLightType.Headlight, node, x);
                return t;
            }).Union(lightsIni.GetSections("BRAKE").Select(x => {
                var t = new CarLight();
                t.Initialize(CarLightType.Brake, node, x);
                return t;
            }));
        }

        public void AdjustPosition(Kn5RenderableList node) {
            node.UpdateBoundingBox();
            node.LocalMatrix = Matrix.Translation(0, -node.BoundingBox?.Minimum.Y ?? 0f, 0) * node.LocalMatrix;
        }

        public void LoadMirrors(Kn5RenderableList node, DeviceContextHolder holder) {
            if (Data.IsEmpty) return;
            foreach (var obj in from section in Data.GetIniFile("mirrors.ini").GetSections("MIRROR")
                                select node.GetByName(section.Get("NAME"))) {
                obj?.SwitchToMirror(holder);
            }
        }

        public void SetKn5(DeviceContextHolder contextHolder) {
            SetupWatching(contextHolder);

            var materialsProvider = contextHolder.Get<Kn5MaterialsProvider>();
            var texturesProvider = contextHolder.Get<TexturesProvider>();

            materialsProvider.SetKn5(MainKn5);
            texturesProvider.SetKn5(MainKn5.OriginalFilename, MainKn5);
            texturesProvider.SetOverridedProvider(MainKn5.OriginalFilename, this);

            if (!string.Equals(MainKn5.OriginalFilename, MainKn5Filename, StringComparison.OrdinalIgnoreCase)) {
                texturesProvider.SetKn5(MainKn5.OriginalFilename, Kn5.FromFile(MainKn5Filename));
            }
        }

        public void Dispose() {
            if (_watcher != null) {
                _watcher.EnableRaisingEvents = false;
                DisposeHelper.Dispose(ref _watcher);
            }

            _holder = null;
        }
    }
}