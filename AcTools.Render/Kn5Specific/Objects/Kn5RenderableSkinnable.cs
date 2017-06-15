using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Kn5Specific.Textures;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5Specific.Objects {
    public class Kn5RenderableSkinnable : Kn5RenderableFile, INotifyPropertyChanged {
        private readonly bool _asyncOverrideTexturesLoading;
        private Kn5OverrideableTexturesProvider _texturesProvider;
        private string _overridesDirectory;

        public Kn5RenderableSkinnable(Kn5 kn5, Matrix matrix, string overridesDirectory, bool asyncTexturesLoading = true,
                bool asyncOverrideTexturesLoading = false, bool allowSkinnedObjects = false) : base(kn5, matrix, asyncTexturesLoading, allowSkinnedObjects) {
            _overridesDirectory = overridesDirectory;
            _asyncOverrideTexturesLoading = asyncOverrideTexturesLoading;
        }

        public bool OverrideTexture(DeviceContextHolder device, string textureName, [CanBeNull] byte[] textureBytes) {
            if (_texturesProvider == null) {
                InitializeTextures(device);
                if (_texturesProvider == null) return false;
            }

            var texture = _texturesProvider.GetTexture(device, textureName);
            texture.SetProceduralOverride(device, textureBytes);
            return texture.Exists;
        }

        public bool OverrideTexture(DeviceContextHolder device, string textureName, [CanBeNull] ShaderResourceView textureView, bool disposeLater) {
            if (_texturesProvider == null) {
                InitializeTextures(device);
                if (_texturesProvider == null) return false;
            }

            var texture = _texturesProvider?.GetTexture(device, textureName);
            texture.SetProceduralOverride(device, textureView, disposeLater);
            return texture.Exists;
        }

        public void ClearProceduralOverrides() {
            if (_texturesProvider == null) return;
            foreach (var texture in _texturesProvider.GetExistingTextures()) {
                texture.SetProceduralOverride(null, null);
            }
        }

        private bool _liveReload;

        public bool LiveReload {
            get { return _liveReload; }
            set {
                if (value == _liveReload) return;
                _liveReload = value;
                OnPropertyChanged();

                if (_texturesProvider != null) {
                    _texturesProvider.LiveReload = value;
                }
            }
        }

        private bool _magickOverride;

        public bool MagickOverride {
            get { return _magickOverride; }
            set {
                if (value == _magickOverride) return;
                _magickOverride = value;
                OnPropertyChanged();

                if (_texturesProvider != null) {
                    _texturesProvider.MagickOverride = value;
                }
            }
        }

        private bool _debugMode;
        private bool? _debugModeLater;

        public bool DebugMode {
            get { return _debugMode; }
            set {
                if (value == _debugMode) return;
                _debugMode = value;
                _debugModeLater = value;
                OnPropertyChanged();
            }
        }

        public override void Draw(IDeviceContextHolder holder, ICamera camera, SpecialRenderMode mode, Func<IRenderableObject, bool> filter = null) {
            if (!IsEnabled) return;

            if (LocalHolder == null) {
                DrawInitialize(holder);
            }

            if (_debugModeLater.HasValue) {
                _debugModeLater = null;

                foreach (var node in this.GetAllChildren().OfType<IKn5RenderableObject>()) {
                    node.SetDebugMode(LocalHolder, _debugMode);
                }
            }

            base.Draw(holder, camera, mode, filter);
        }

        protected override ITexturesProvider InitializeTextures(IDeviceContextHolder contextHolder) {
            _texturesProvider = new Kn5SkinnableTexturesProvider(OriginalFile, AsyncTexturesLoading, _asyncOverrideTexturesLoading) {
                LiveReload = LiveReload,
                MagickOverride = MagickOverride
            };

            if (_overridesDirectory != null) {
                _texturesProvider.SetOverridesDirectory(contextHolder, _overridesDirectory);
            }

            return _texturesProvider;
        }

        public void ClearOverridesDirectory() {
            if (_texturesProvider == null) {
                _overridesDirectory = null;
                return;
            }

            _texturesProvider.ClearOverridesDirectory();
        }

        public void SetOverridesDirectory(IDeviceContextHolder contextHolder, string directory) {
            if (_texturesProvider == null) {
                _overridesDirectory = directory;
                return;
            }

            if (directory == null) {
                _texturesProvider.ClearOverridesDirectory();
            } else {
                _texturesProvider.SetOverridesDirectory(contextHolder, directory);
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}