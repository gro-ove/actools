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
using AcTools.Utils;
using AcTools.Utils.Helpers;
using SlimDX;

namespace AcTools.Render.Kn5Specific.Utils {
    public class CarHelper : IOverridedTextureProvider, IDisposable {
        public readonly Kn5 MainKn5;
        public readonly string RootDirectory;
        public readonly string SkinsDirectory;
        public readonly string MainKn5Filename;
        public readonly DataWrapper Data;

        public List<string> Skins { get; private set; }

        public CarHelper(Kn5 kn5) {
            MainKn5 = kn5;

            RootDirectory = Path.GetDirectoryName(kn5.OriginalFilename);
            SkinsDirectory = FileUtils.GetCarSkinsDirectory(RootDirectory);
            Data = DataWrapper.FromFile(RootDirectory);

            MainKn5Filename = FileUtils.GetMainCarFilename(RootDirectory, Data);
            ReloadSkins();
        }

        private FileSystemWatcher _watcher;
        private DeviceContextHolder _holder;

        public void SetupWatching(DeviceContextHolder holder) {
            if (Directory.Exists(SkinsDirectory)) {
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
        }

        private async void UpdateOverrideLater(string filename) {
            var splitted = FileUtils.GetRelativePath(filename, SkinsDirectory).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (splitted.Length != 2) return;

            var skinId = splitted[0];
            var textureName = splitted[1];
            if (!string.Equals(CurrentSkin, skinId, StringComparison.OrdinalIgnoreCase)) return;

            var texture = TexturesProvider.GetFor(MainKn5).OfType<RenderableTexture>().FirstOrDefault(x =>
                    string.Equals(x.Name, textureName, StringComparison.OrdinalIgnoreCase));
            if (texture == null) return;

            await Task.Delay(200).ConfigureAwait(false);
            texture.LoadOverrideAsync(filename, _holder.Device);
        }

        private async void ReloadSkinsLater(string selectSkin = null) {
            await Task.Delay(200).ConfigureAwait(false);
            ReloadSkins(selectSkin);
        }

        private void Watcher_Changed(object sender, FileSystemEventArgs e) {
            UpdateOverrideLater(e.FullPath);
        }

        private void Watcher_Created(object sender, FileSystemEventArgs e) {
            var splitted = FileUtils.GetRelativePath(e.FullPath, SkinsDirectory).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (splitted.Length == 1) {
                ReloadSkinsLater();
            } else if (splitted.Length == 2) {
                UpdateOverrideLater(e.FullPath);
            }
        }

        private void Watcher_Deleted(object sender, FileSystemEventArgs e) {
            var splitted = FileUtils.GetRelativePath(e.FullPath, SkinsDirectory).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (splitted.Length == 1) {
                ReloadSkinsLater();
            } else if (splitted.Length == 2) {
                UpdateOverrideLater(e.FullPath);
            }
        }

        private void Watcher_Renamed(object sender, RenamedEventArgs e) {
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

        public string CurrentSkin { get; private set; }

        private void ReloadSkins(string selectSkin = null) {
            var selectedIndex = Skins?.IndexOf(CurrentSkin);

            try {
                Skins = Directory.GetDirectories(SkinsDirectory).Select(Path.GetFileName).ToList();
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
            SwitchSkinTo(index < 0 || index >= Skins.Count - 1 ? Skins[0] : Skins[index + 1], contextHolder);
        }

        public void SelectPreviousSkin(DeviceContextHolder contextHolder) {
            if (Skins.Any() != true) return;

            var index = Skins.IndexOf(CurrentSkin);
            SwitchSkinTo(index <= 0 ? Skins[Skins.Count - 1] : Skins[index - 1], contextHolder);
        }

        public string GetOverridedFilename(string name) {
            if (CurrentSkin == null) return null;
            
            var filename = Path.Combine(SkinsDirectory, CurrentSkin, name);
            return File.Exists(filename) ? filename : null;
        }

        public void SwitchSkinTo(string skinId, DeviceContextHolder contextHolder) {
            CurrentSkin = skinId;
            TexturesProvider.UpdateOverridesFor(MainKn5, contextHolder);
        }

        private IRenderableObject LoadWheelAmbientShadow(Kn5RenderableList main, string nodeName, string textureName) {
            var node = main.GetDummyByName(nodeName);
            if (node == null) return null;

            var wheel = node.Matrix.GetTranslationVector();
            wheel.Y = 0.01f;

            var filename = Path.Combine(RootDirectory, textureName);
            return new AmbientShadow(filename, Matrix.Scaling(0.3f, 1.0f, 0.3f) * Matrix.RotationY(MathF.PI) *
                    Matrix.Translation(wheel));
        }
        
        private IRenderableObject LoadBodyAmbientShadow() {
            var iniFile = Data.GetIniFile("ambient_shadows.ini");
            var ambientBodyShadowSize = new Vector3(
                    (float)iniFile["SETTINGS"].GetDouble("WIDTH", 1d), 1.0f,
                    (float)iniFile["SETTINGS"].GetDouble("LENGTH", 1d));

            var filename = Path.Combine(RootDirectory, "body_shadow.png");
            return new AmbientShadow(filename, Matrix.Scaling(ambientBodyShadowSize) * Matrix.RotationY(MathF.PI) *
                    Matrix.Translation(0f, 0.01f, 0f));
        }

        public IEnumerable<IRenderableObject> LoadAmbientShadows(Kn5RenderableList node) {
            if (Data.IsEmpty) return new IRenderableObject[0];
            return new[] {
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

        public void LoadMirrors(Kn5RenderableList node) {
            if (Data.IsEmpty) return;
            foreach (var obj in from section in Data.GetIniFile("mirrors.ini").GetSections("MIRROR")
                                select node.GetByName(section.Get("NAME"))) {
                obj?.SwitchToMirror();
            }
        }

        public void SetKn5(DeviceContextHolder contextHolder) {
            SetupWatching(contextHolder);

            Kn5MaterialsProvider.SetKn5(MainKn5);
            TexturesProvider.SetKn5(MainKn5.OriginalFilename, MainKn5);
            TexturesProvider.SetOverridedProvider(MainKn5.OriginalFilename, this);

            if (!string.Equals(MainKn5.OriginalFilename, MainKn5Filename, StringComparison.OrdinalIgnoreCase)) {
                TexturesProvider.SetKn5(MainKn5.OriginalFilename, Kn5.FromFile(MainKn5Filename));
            }
        }

        public void Dispose() {
            if (_watcher != null) {
                _watcher.EnableRaisingEvents = false;
                DisposeHelper.Dispose(ref _watcher);
            }

            _holder = null;

            TexturesProvider.SetOverridedProvider(MainKn5.OriginalFilename, null);
            TexturesProvider.DisposeFor(MainKn5);
            Kn5MaterialsProvider.DisposeFor(MainKn5);
        }
    }
}