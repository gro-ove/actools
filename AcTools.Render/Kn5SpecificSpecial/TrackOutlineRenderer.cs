using System;
using System.IO;
using System.Linq;
using AcTools.AiFile;
using AcTools.DataFile;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Kn5SpecificSpecial {
    public class TrackOutlineRenderer : BaseRenderer {
        private readonly string[] _mapFilenames;
        private readonly string _previewFilename;

        public bool IsAiLanesModeAvailable { get; }

        protected override FeatureLevel FeatureLevel => FeatureLevel.Level_10_0;

        public TrackOutlineRenderer(string[] mapFilenames, string currentMapFilename, string previewFilename) {
            _mapFilenames = mapFilenames;
            CurrentMapFilename = currentMapFilename;
            IsAiLanesModeAvailable = mapFilenames.All(x => File.Exists(GetAiLaneFastFilename(x)));

            _previewFilename = previewFilename;
            ResolutionMultiplier = 4d;
        }

        public bool LoadPreview { get; set; } = true;

        [CanBeNull]
        private string[] _activeMaps;

        public void SetActiveMaps([CanBeNull] string[] active) {
            _activeMaps = active;
            IsDirty = true;
        }

        private string _currentMapFilename;

        public string CurrentMapFilename {
            get { return _currentMapFilename; }
            set {
                if (Equals(value, _currentMapFilename)) return;
                _currentMapFilename = value;
                OnPropertyChanged();
                IsDirty = true;
            }
        }

        private bool _useAiLanes = true;

        public bool UseAiLanes {
            get { return _useAiLanes && IsAiLanesModeAvailable; }
            set {
                if (Equals(value, UseAiLanes)) return;
                _useAiLanes = value;
                OnPropertyChanged();
                IsDirty = true;

                foreach (var map in _maps) {
                    map.AiLaneMode = value && IsAiLanesModeAvailable;
                }
            }
        }

        private bool _useFxaa = true;

        public bool UseFxaa {
            get { return _useFxaa; }
            set {
                if (Equals(value, _useFxaa)) return;
                _useFxaa = value;
                OnPropertyChanged();
                IsDirty = true;
            }
        }

        private float _rotation;

        public float Rotation {
            get { return _rotation; }
            set {
                if (Equals(value, _rotation)) return;
                _rotation = value;
                OnPropertyChanged();
                IsDirty = true;
            }
        }

        private float _extraWidth = 0.5f;

        public float ExtraWidth {
            get { return _extraWidth; }
            set {
                if (Equals(value, _extraWidth)) return;
                _extraWidth = value;
                OnPropertyChanged();
                IsDirty = true;
            }
        }

        private float _offsetX;

        public float OffsetX {
            get { return _offsetX; }
            set {
                if (Equals(value, _offsetX)) return;
                _offsetX = value;
                OnPropertyChanged();
                IsDirty = true;
            }
        }

        private float _offsetY;

        public float OffsetY {
            get { return _offsetY; }
            set {
                if (Equals(value, _offsetY)) return;
                _offsetY = value;
                OnPropertyChanged();
                IsDirty = true;
            }
        }

        private float _scale = 0.82f;

        public float Scale {
            get { return _scale; }
            set {
                if (Equals(value, _scale)) return;

                OffsetX = OffsetX / _scale * value;
                OffsetY = OffsetY / _scale * value;

                _scale = value;
                OnPropertyChanged();
                IsDirty = true;
            }
        }

        private float _shadowDistance = 1f;

        public float ShadowDistance {
            get { return _shadowDistance; }
            set {
                if (Equals(value, _shadowDistance)) return;
                _shadowDistance = value;
                OnPropertyChanged();
                IsDirty = true;
            }
        }

        private float _shadowOpacity = 0.75f;

        public float ShadowOpacity {
            get { return _shadowOpacity; }
            set {
                if (Equals(value, _shadowOpacity)) return;
                _shadowOpacity = value;
                OnPropertyChanged();
                IsDirty = true;
            }
        }

        private float _dimmedOpacity = 0.64f;

        public float DimmedOpacity {
            get { return _dimmedOpacity; }
            set {
                if (Equals(value, _dimmedOpacity)) return;
                _dimmedOpacity = value;
                OnPropertyChanged();
                IsDirty = true;
            }
        }

        private float _dimmedWidthMultipler = 1f;

        public float DimmedWidthMultipler {
            get { return _dimmedWidthMultipler; }
            set {
                if (Equals(value, _dimmedWidthMultipler)) return;
                _dimmedWidthMultipler = value;
                OnPropertyChanged();
                IsDirty = true;
            }
        }

        private static string GetAiLaneFastFilename(string mapFilename) {
            return Path.Combine(Path.GetDirectoryName(mapFilename) ?? "", "ai", "fast_lane.ai");
        }

        private class MapViewData : IDisposable {
            public string Filename { get; }

            public Vector2 MapSize { get; set; }

            public Vector2 DataSize { get; }

            public Vector2 DataOffset { get; }

            public float DataMargin { get; }

            public float DataScale { get; }

            public MapViewData(IDeviceContextHolder holder, string mapFilename, bool aiLaneMode) {
                Filename = mapFilename;
                AiLaneMode = aiLaneMode;
                Initialize(holder);

                var data = new IniFile(Path.Combine(Path.GetDirectoryName(mapFilename) ?? "", "data", "map.ini"))["PARAMETERS"];
                DataSize = new Vector2(data.GetFloat("WIDTH", 100), data.GetFloat("HEIGHT", 100));
                DataOffset = new Vector2(data.GetFloat("X_OFFSET", 0), data.GetFloat("Z_OFFSET", 0));
                DataMargin = data.GetFloat("MARGIN", 20f);
                DataScale = data.GetFloat("SCALE_FACTOR", 1f);
            }

            private bool _aiLaneMode;

            public bool AiLaneMode {
                get { return _aiLaneMode; }
                set {
                    if (Equals(_aiLaneMode, value)) return;
                    _aiLaneMode = value;
                    _dirty = true;
                }
            }

            private bool _dirty;
            private ShaderResourceView _view;
            private IRenderableObject _obj;
            private AiLane _lane;

            private void Initialize(IDeviceContextHolder holder) {
                // TODO: errors handling!

                // DisposeHelper.Dispose(ref _view);
                DisposeHelper.Dispose(ref _obj);

                _dirty = false;
                if (AiLaneMode) {
                    if (_lane == null) {
                        _lane = AiLane.FromFile(GetAiLaneFastFilename(Filename));
                    }

                    _obj = AiLaneObject.Create(_lane, AiLaneWidth);
                    _obj.ParentMatrix = Matrix.Identity;
                    _obj.UpdateBoundingBox();

                    if (MapSize == default(Vector2)) {
                        var size = (_obj.BoundingBox ?? default(BoundingBox)).GetSize();
                        MapSize = new Vector2(size.X, size.Z);
                    }
                } else {
                    if (_view == null) {
                        using (var map = Texture2D.FromFile(holder.Device, Filename)) {
                            if (MapSize == default(Vector2)) {
                                MapSize = new Vector2(map.Description.Width, map.Description.Height);
                            }

                            _view = new ShaderResourceView(holder.Device, map);
                        }
                    }
                }
            }

            private bool _materialsSet;

            public class TrackOutlineMaterialsFactory : IMaterialsFactory {
                public IRenderableMaterial CreateMaterial(object key) {
                    if (BasicMaterials.DepthOnlyKey.Equals(key)) {
                        return new Kn5MaterialTrackOutline();
                    }

                    return new InvisibleMaterial();
                }
            }

            private class Kn5MaterialTrackOutline : IRenderableMaterial {
                private EffectSpecialTrackOutline _effect;
                internal static Matrix Matrix;

                public void Initialize(IDeviceContextHolder contextHolder) {
                    _effect = contextHolder.GetEffect<EffectSpecialTrackOutline>();
                }

                public bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
                    if (mode != SpecialRenderMode.Simple) return false;
                    contextHolder.DeviceContext.InputAssembler.InputLayout = _effect.LayoutP;
                    contextHolder.DeviceContext.OutputMerger.BlendState = null;
                    return true;
                }

                public void SetMatrices(Matrix objectTransform, ICamera camera) {
                    var toUv = Matrix.Transformation2D(Vector2.Zero, 0f, new Vector2(0.5f), Vector2.Zero, 0f, new Vector2(0.5f));
                    _effect.FxWorldViewProj.SetMatrix(objectTransform * camera.ViewProj * toUv * Matrix * Matrix.Invert(toUv));
                }

                public void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
                    _effect.TechFirstStepObj.DrawAllPasses(contextHolder.DeviceContext, indices);
                }

                public bool IsBlending => false;

                public void Dispose() { }
            }

            private CameraOrtho _camera;

            private CameraOrtho GetCamera(DeviceContextHolder holder) {
                if (_camera == null) {
                    if (_dirty) {
                        Initialize(holder);
                    }

                    var laneBox = _obj.BoundingBox ?? new BoundingBox();

                    var boxWidth = DataSize.X;
                    var boxHeight = DataSize.Y;
                    var boxMinX = -DataOffset.X + DataMargin;
                    var boxMinY = -DataOffset.Y + DataMargin;

                    var box = new BoundingBox(
                            new Vector3(boxMinX, laneBox.Minimum.Y, boxMinY),
                            new Vector3(boxMinX + boxWidth, laneBox.Maximum.Y, boxMinY + boxHeight));

                    _camera = new CameraOrtho {
                        Position = new Vector3(box.GetCenter().X, box.Maximum.Y + 1f, box.GetCenter().Z),
                        FarZ = box.GetSize().Y + 2f,
                        Target = box.GetCenter(),
                        Up = new Vector3(0.0f, 0.0f, -1.0f),
                        Width = box.GetSize().X + 2 * DataMargin,
                        Height = box.GetSize().Z + 2 * DataMargin,
                        DisableFrustum = true
                    };

                    _camera.SetLens();
                    _camera.UpdateViewMatrix();
                }

                return _camera;
            }

            public void Draw(DeviceContextHolder holder, EffectSpecialTrackOutline effect, Matrix mapMatrix, MapViewData max) {
                if (_dirty) {
                    Initialize(holder);
                }
                
                if (AiLaneMode) {
                    Kn5MaterialTrackOutline.Matrix = mapMatrix;

                    if (!_materialsSet) {
                        _materialsSet = true;
                        if (holder.TryToGet<IMaterialsFactory>() == null) {
                            holder.Set<IMaterialsFactory>(new TrackOutlineMaterialsFactory());
                        }
                    }

                    _obj.Draw(holder, max?.GetCamera(holder) ?? GetCamera(holder), SpecialRenderMode.Simple);

                    // revert quad
                    holder.PrepareQuad(effect.LayoutPT);
                } else {
                    // local transformation matrix: global×local offset (calculated from map.ini)×local scale
                    var localScale = Matrix.Transformation2D(Vector2.Zero, 0f,
                            new Vector2(max.DataSize.X / DataSize.X, max.DataSize.Y / DataSize.Y) / DataScale,
                            Vector2.Zero, 0f, Vector2.Zero);
                    var localOffset = Matrix.AffineTransformation2D(1f, Vector2.Zero, 0f, new Vector2(
                            (DataOffset.X - max.DataOffset.X) / max.DataSize.X,
                            (DataOffset.Y - max.DataOffset.Y) / max.DataSize.Y));
                    effect.FxMatrix.SetMatrix(mapMatrix * localOffset * localScale);
                    effect.FxInputMap.SetResource(_view);
                    effect.TechFirstStep.DrawAllPasses(holder.DeviceContext, 6);
                }
            }

            private float _aiLaneWidth = 15f;

            public float AiLaneWidth {
                get { return _aiLaneWidth; }
                set {
                    if (Equals(value, _aiLaneWidth)) return;
                    _aiLaneWidth = value;
                    _dirty = true;
                }
            }

            public void Dispose() {
                DisposeHelper.Dispose(ref _view);
                DisposeHelper.Dispose(ref _obj);
            }
        }

        private MapViewData[] _maps;
        private Vector2 _previewSize;
        private ShaderResourceView _previewView;
        private TargetResourceTexture _f0Buffer, _f1Buffer, _fBlendBuffer, _fSummBuffer, _a0Buffer, _a1Buffer, _aSummBuffer;
        private BlendState _combineBlendState;
        private EffectSpecialTrackOutline _effect;

        protected override void InitializeInner() {
            _effect = DeviceContextHolder.GetEffect<EffectSpecialTrackOutline>();

            _maps = _mapFilenames.Select((x, i) => {
                var data = new MapViewData(DeviceContextHolder, x, UseAiLanes);
                if (i == 0) {
                    Scale *= (data.MapSize.X / data.MapSize.Y).Clamp(1f, 2f);
                }

                return data;
            }).ToArray();

            if (LoadPreview) {
                if (File.Exists(_previewFilename)) {
                    using (var preview = Texture2D.FromFile(Device, _previewFilename)) {
                        _previewSize = new Vector2(preview.Description.Width, preview.Description.Height);
                        _previewView = new ShaderResourceView(Device, preview);
                    }
                } else {
                    AcToolsLogging.Write("Not found: " + _previewFilename);
                }
            }

            _f0Buffer = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);
            _f1Buffer = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);
            _fBlendBuffer = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);
            _fSummBuffer = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);
            _a0Buffer = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);
            _a1Buffer = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);
            _aSummBuffer = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);

            _combineBlendState = Device.CreateBlendState(new RenderTargetBlendDescription {
                BlendEnable = true,
                SourceBlend = BlendOption.SourceAlpha,
                DestinationBlend = BlendOption.InverseSourceAlpha,
                BlendOperation = BlendOperation.Add,
                SourceBlendAlpha = BlendOption.One,
                DestinationBlendAlpha = BlendOption.One,
                BlendOperationAlpha = BlendOperation.Maximum,
                RenderTargetWriteMask = ColorWriteMaskFlags.All
            });
        }

        private bool _shotMode;

        public void Shot(string outlineImage) {
            try {
                _shotMode = true;

                Draw();
                Texture2D.ToFile(DeviceContext, RenderBuffer, ImageFileFormat.Png, outlineImage);
            } finally {
                _shotMode = false;
                IsDirty = true;
            }
        }

        protected override void ResizeInner() {
            _f0Buffer.Resize(DeviceContextHolder, Width, Height, null);
            _f1Buffer.Resize(DeviceContextHolder, Width, Height, null);
            _fBlendBuffer.Resize(DeviceContextHolder, Width, Height, null);
            _fSummBuffer.Resize(DeviceContextHolder, Width, Height, null);
            _a0Buffer.Resize(DeviceContextHolder, ActualWidth, ActualHeight, null);
            _a1Buffer.Resize(DeviceContextHolder, ActualWidth, ActualHeight, null);
            _aSummBuffer.Resize(DeviceContextHolder, ActualWidth, ActualHeight, null);
        }

        private void DrawMap(MapViewData map, MapViewData max, Matrix global) {
            // is the main map?
            var isMain = FileUtils.ArePathsEqual(map.Filename, CurrentMapFilename);

            // reset states
            DeviceContext.OutputMerger.BlendState = null;
            DeviceContext.Rasterizer.SetViewports(Viewport);

            // clear buffers just in case
            DeviceContext.ClearRenderTargetView(_f0Buffer.TargetView, new Color4(0f, 0f, 0f, 0f));
            DeviceContext.ClearRenderTargetView(_f1Buffer.TargetView, new Color4(0f, 0f, 0f, 0f));
            DeviceContext.ClearRenderTargetView(_a0Buffer.TargetView, new Color4(0f, 0f, 0f, 0f));
            DeviceContext.ClearRenderTargetView(_a1Buffer.TargetView, new Color4(0f, 0f, 0f, 0f));

            // set width to ai lanes
            if (UseAiLanes) {
                map.AiLaneWidth = 25f * ((isMain ? ExtraWidth : ExtraWidth * DimmedWidthMultipler) * 0.7f + 0.15f);
            }

            // draw map
            DeviceContext.OutputMerger.SetTargets(_f0Buffer.TargetView);
            map.Draw(DeviceContextHolder, _effect, global, max);

            // pp
            var current = _f0Buffer;

            // expand its width if needed
            if (!UseAiLanes) {
                var extraWidth = isMain ? ExtraWidth : ExtraWidth * DimmedWidthMultipler;
                for (var i = 0; i < extraWidth; i++) {
                    var next = current == _f0Buffer ? _f1Buffer : _f0Buffer;
                    DeviceContext.OutputMerger.SetTargets(next.TargetView);
                    _effect.FxInputMap.SetResource(current.View);

                    if (i < extraWidth - 1) {
                        _effect.FxExtraWidth.Set(3f);
                    } else {
                        var last = extraWidth % 1f;
                        _effect.FxExtraWidth.Set(last < 0.0001f ? 3f : last * 3f);
                    }

                    _effect.TechExtraWidth.DrawAllPasses(DeviceContext, 6);
                    current = next;
                }
            }

            // fxaa if needed
            if (UseFxaa) {
                var next = current == _f0Buffer ? _f1Buffer : _f0Buffer;
                DeviceContextHolder.GetHelper<FxaaHelper>().Draw(DeviceContextHolder, current.View, next.TargetView);
                current = next;
            }

            // shadow if needed
            if (ShadowDistance > 0f && ShadowOpacity > 0f) {
                var next = current == _f0Buffer ? _f1Buffer : _f0Buffer;
                DeviceContext.OutputMerger.SetTargets(next.TargetView);
                _effect.FxInputMap.SetResource(current.View);
                _effect.TechShadow.DrawAllPasses(DeviceContext, 6);
                CustomBlending(_aSummBuffer, next, _a1Buffer, new Color4((isMain ? 1f : DimmedOpacity) * ShadowOpacity, 0f, 0f, 0f));
            }

            CustomBlending(_aSummBuffer, current, _a1Buffer, new Color4(isMain ? 1f : DimmedOpacity, 1f, 1f, 1f));
        }

        // slower, but more accurate
        private void CustomBlending(TargetResourceTexture result, TargetResourceTexture foreground, TargetResourceTexture temporary, 
                Color4 color) {
            // ssaa if needed
            ShaderResourceView view;
            if (UseSsaa) {
                DeviceContextHolder.GetHelper<DownsampleHelper>().Draw(DeviceContextHolder, foreground, _a0Buffer);
                view = _a0Buffer.View;
            } else {
                view = foreground.View;
            }

            DeviceContextHolder.GetHelper<CopyHelper>().Draw(DeviceContextHolder, result.View, temporary.TargetView);

            DeviceContextHolder.PrepareQuad(_effect.LayoutPT);
            DeviceContext.OutputMerger.SetTargets(result.TargetView);

            _effect.FxInputMap.SetResource(view);
            _effect.FxBgMap.SetResource(temporary.View);
            _effect.FxBlendColor.Set(color);
            _effect.TechBlend.DrawAllPasses(DeviceContext, 6);
        }

        protected override void DrawOverride() {
            // prepare states, clear buffers
            DeviceContext.OutputMerger.DepthStencilState = null;
            DeviceContext.Rasterizer.State = DeviceContextHolder.States.DoubleSidedState;
            DeviceContext.ClearRenderTargetView(_fSummBuffer.TargetView, new Color4(0f, 0f, 0f, 0f));
            DeviceContext.ClearRenderTargetView(_aSummBuffer.TargetView, new Color4(0f, 0f, 0f, 0f));
            DeviceContext.ClearRenderTargetView(RenderTargetView, new Color4(0f, 0f, 0f, 0f));

            // quad
            DeviceContextHolder.PrepareQuad(_effect.LayoutPT);

            // fixed values
            _effect.FxScreenSize.Set(new Vector4(Width, Height, 1f / Width, 1f / Height));
            _effect.FxDropShadowRadius.Set((float)(ShadowDistance * ResolutionMultiplier));
            // _effect.FxDropShadowOpacity.Set(ShadowOpacity);

            // find the biggest map provided
            MapViewData max;
            float maxSize;
            {
                var maxMapSize = _maps.Select(x => new {
                    MaxSize = Math.Max(x.MapSize.X, x.MapSize.Y),
                    Entry = x
                }).MaxEntryOrDefault(x => x.MaxSize);
                if (maxMapSize == null) return;

                max = maxMapSize.Entry;
                maxSize = maxMapSize.MaxSize;
            }

            // calculate basic UV transformation matrix which will be applied to all maps
            Matrix global;
            {
                // first of all fix aspect to match map.png and scale
                var mapScale = UseAiLanes ?
                        Matrix.Transformation2D(new Vector2(0.5f), 0f,
                                new Vector2(max.MapSize.X, max.MapSize.Y) / maxSize * Scale,
                                Vector2.Zero, 0f, Vector2.Zero) :
                        Matrix.Transformation2D(new Vector2(0.5f), 0f,
                                new Vector2(1f / max.MapSize.X, 1f / max.MapSize.Y) * maxSize / Scale,
                                Vector2.Zero, 0f, Vector2.Zero);

                // fix aspect (without it, maps will be stretched to Renderer dimensions)
                var minSide = (float)Math.Min(Width, Height);
                var scale = UseAiLanes ?
                        Matrix.Transformation2D(new Vector2(0.5f), 0f, new Vector2(minSide / Width, minSide / Height), Vector2.Zero, 0f, Vector2.Zero) :
                        Matrix.Transformation2D(new Vector2(0.5f), 0f, new Vector2(Width, Height) / minSide, Vector2.Zero, 0f, Vector2.Zero);

                // rotate if needed
                var rotation = Matrix.Transformation2D(Vector2.Zero, 0f, new Vector2(1f), new Vector2(0.5f), -Rotation, Vector2.Zero);

                // optional offset
                var offset = UseAiLanes ?
                        Matrix.AffineTransformation2D(1f, Vector2.Zero, 0f, new Vector2(-OffsetX / Width, OffsetY / Height)) :
                        Matrix.AffineTransformation2D(1f, Vector2.Zero, 0f, new Vector2(OffsetX / Width, OffsetY / Height));
                
                global = UseAiLanes ? mapScale * rotation * scale * offset : offset * scale * rotation * mapScale;
            }

            foreach (var map in _maps.Where(x => !FileUtils.ArePathsEqual(x.Filename, CurrentMapFilename) && (_activeMaps == null ||
                    _activeMaps.Any(y => FileUtils.ArePathsEqual(x.Filename, y))))) {
                DrawMap(map, max, global);
            }

            foreach (var map in _maps.Where(x => FileUtils.ArePathsEqual(x.Filename, CurrentMapFilename))) {
                DrawMap(map, max, global);
            }

            // output state
            DeviceContext.Rasterizer.SetViewports(OutputViewport);

            // draw result (on preview image if exists)
            DeviceContext.OutputMerger.SetTargets(RenderTargetView);
            _effect.FxInputMap.SetResource(_aSummBuffer.View);

            if (_shotMode) {
                _effect.TechFinal.DrawAllPasses(DeviceContext, 6);
            } else if (_previewView != null) {
                _effect.FxBgMap.SetResource(_previewView);

                var bgScale = new Vector2(ActualWidth / _previewSize.X, ActualHeight / _previewSize.Y);
                if (bgScale.X > 1f) bgScale /= bgScale.X;
                if (bgScale.Y > 1f) bgScale /= bgScale.Y;

                _effect.FxMatrix.SetMatrix(Matrix.Transformation2D(new Vector2(0.5f), 0f, bgScale, Vector2.Zero, 0f, Vector2.Zero));
                _effect.TechFinalBg.DrawAllPasses(DeviceContext, 6);
            } else {
                _effect.TechFinalCheckers.DrawAllPasses(DeviceContext, 6);
            }
        }

        protected override void OnTick(float dt) {}

        protected override void DisposeOverride() {
            _maps?.DisposeEverything();
            DisposeHelper.Dispose(ref _previewView);
            DisposeHelper.Dispose(ref _f0Buffer);
            DisposeHelper.Dispose(ref _f1Buffer);
            DisposeHelper.Dispose(ref _fBlendBuffer);
            DisposeHelper.Dispose(ref _fSummBuffer);
            DisposeHelper.Dispose(ref _a0Buffer);
            DisposeHelper.Dispose(ref _a1Buffer);
            DisposeHelper.Dispose(ref _aSummBuffer);
            DisposeHelper.Dispose(ref _combineBlendState);
            base.DisposeOverride();
        }
    }
}