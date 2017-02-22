using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using AcTools.Kn5File;
using AcTools.KnhFile;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Textures;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Kn5Specific.Objects {
    public class Kn5RenderableDriver : Kn5RenderableFile, INotifyPropertyChanged {
        private readonly bool _asyncOverrideTexturesLoading;
        private Kn5OverrideableTexturesProvider _texturesProvider;
        private string _overridesDirectory;

        public Kn5RenderableDriver(Kn5 kn5, Matrix matrix, string overridesDirectory, bool asyncTexturesLoading = true,
                bool asyncOverrideTexturesLoading = false, bool allowSkinnedObjects = false) : base(kn5, matrix, asyncTexturesLoading, allowSkinnedObjects) {
            _overridesDirectory = overridesDirectory;
            _asyncOverrideTexturesLoading = asyncOverrideTexturesLoading;
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

        public override void Draw(IDeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode, Func<IRenderableObject, bool> filter = null) {
            if (_debugModeLater.HasValue) {
                _debugModeLater = null;

                foreach (var node in this.GetAllChildren().OfType<IKn5RenderableObject>()) {
                    node.SetDebugMode(LocalHolder, _debugMode);
                }
            }

            base.Draw(contextHolder, camera, mode, filter);
        }

        protected override ITexturesProvider InitializeTextures(IDeviceContextHolder contextHolder) {
            _texturesProvider = new Kn5OverrideableTexturesProvider(OriginalFile, AsyncTexturesLoading, _asyncOverrideTexturesLoading) {
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

        private void AlignNodes(KnhEntry entry, Matrix matrix) {
            var dummy = GetDummyByName(entry.Name);
            if (dummy != null) {
                dummy.LocalMatrix = entry.Transformation.ToMatrix();
            } else {
                matrix = entry.Transformation.ToMatrix() * matrix;
            }

            foreach (var child in entry.Children) {
                AlignNodes(child, matrix);
            }
        }

        public void AlignNodes(Knh node) {
            AlignNodes(node.RootEntry, Matrix.Identity);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}