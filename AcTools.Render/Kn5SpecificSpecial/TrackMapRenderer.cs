using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using AcTools.AiFile;
using AcTools.DataFile;
using AcTools.Kn5File;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Materials;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.PostEffects;
using AcTools.Render.Base.Sprites;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Base.Utils;
using AcTools.Render.Data;
using AcTools.Render.Kn5Specific;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Shaders;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DirectWrite;
using SlimDX.DXGI;
using FontStyle = SlimDX.DirectWrite.FontStyle;
using Matrix = SlimDX.Matrix;
using TextAlignment = AcTools.Render.Base.Sprites.TextAlignment;

namespace AcTools.Render.Kn5SpecificSpecial {
    public class TrackMapPreparationRenderer : TrackMapRenderer, IKn5ObjectRenderer {
        private TextBlockRenderer _textBlock;

        public bool LockCamera => false;

        public TrackMapPreparationRenderer(string mainKn5Filename) : base(mainKn5Filename) {
            Camera = new CameraOrtho();
        }

        public TrackMapPreparationRenderer(Kn5 kn5) : base(kn5) {
            Camera = new CameraOrtho();
        }

        public TrackMapPreparationRenderer(AiLane kn5) : base(kn5) {
            Camera = new CameraOrtho();
        }

        public TrackMapPreparationRenderer(TrackComplexModelDescription description) : base(description) {
            Camera = new CameraOrtho();
        }

        protected override void InitializeInner() {
            base.InitializeInner();
            UpdateFiltered();
            ResetCamera();
            IsDirty = true;
        }

        protected override void DisposeOverride() {
            DisposeHelper.Dispose(ref _textBlock);
            base.DisposeOverride();
        }

        public void Update() {
            UpdateFiltered();
            if (AutoResetCamera) {
                ResetCamera();
            }

            IsDirty = true;
        }

        private float _zoom;

        public float Zoom {
            get { return _zoom; }
            private set {
                if (Equals(value, _zoom)) return;
                _zoom = value;
                OnPropertyChanged();
            }
        }

        public void SetZoom(float zoom) {
            zoom = zoom.Clamp(0.00001f, 1000f);

            if (Equals(zoom, Zoom)) return;
            Zoom = zoom;

            var camera = CameraOrtho;
            if (camera != null) {
                camera.Width = Width / Zoom;
                camera.Height = Height / Zoom;
                camera.SetLens();
                IsDirty = true;
            }
        }

        protected override CameraOrtho GetCamera() {
            var result = base.GetCamera();
            if (result.Width > 0 && result.Height > 0) {
                var zoom = Math.Min(Width / result.Width, Height / result.Height);
                result.Width = Width / zoom;
                result.Height = Height / zoom;
            }

            return result;
        }

        public CameraOrbit CameraOrbit => Camera as CameraOrbit;

        public FpsCamera FpsCamera => Camera as FpsCamera;

        public CameraOrtho CameraOrtho => Camera as CameraOrtho;

        public bool AutoResetCamera { get; set; } = true;

        public bool AutoRotate { get; set; }

        public bool AutoAdjustTarget { get; set; }

        public bool UseFpsCamera { get; set; }

        public bool VisibleUi { get; set; } = true;

        public bool CarLightsEnabled { get; set; }

        public bool CarBrakeLightsEnabled { get; set; }

        public void SelectPreviousSkin() {}

        public void SelectNextSkin() {}

        public void SelectSkin(string skinId) {}

        void IKn5ObjectRenderer.ResetCamera() {
            ResetCamera();
        }

        public void ChangeCameraFov(float newFovY) {}

        protected sealed override void DrawSprites() {
            var sprite = Sprite;
            if (sprite == null || ShotMode) return;
            DrawSpritesInner();
            sprite.Flush();
        }

        protected virtual void DrawSpritesInner() {
            if (!VisibleUi) return;

            if (_textBlock == null) {
                _textBlock = new TextBlockRenderer(Sprite, "Arial", FontWeight.Normal, FontStyle.Normal, FontStretch.Normal, 20f);
            }

            if (TrianglesCount == 0) {
                _textBlock.DrawString("Nothing found",
                        new RectangleF(0, 0, Width, Height), TextAlignment.VerticalCenter | TextAlignment.HorizontalCenter, 20f,
                        new Color4(1.0f, 1.0f, 1.0f), CoordinateType.Absolute);
            } else {
                _textBlock.DrawString($"Triangles: {TrianglesCount}\nZoom: {Zoom:F3}×\nResult image most likely will be different size",
                        new RectangleF(8, 8, Width - 16, Height - 16), TextAlignment.Bottom | TextAlignment.Left, 12f,
                        new Color4(1.0f, 1.0f, 1.0f), CoordinateType.Absolute);
            }
        }

        protected override void ResetCamera() {
            AutoResetCamera = true;
            base.ResetCamera();
            IsDirty = true;

            var camera = CameraOrtho;
            if (camera == null) {
                Zoom = 1f;
            } else {
                Zoom = Math.Min(Width / camera.Width, Height / camera.Height);
            }
        }

        public void MoveCameraToStart() {
            var camera = base.GetCamera();
            var node = RootNode.GetDummyByName("AC_START_0");
            var vector = node?.Matrix.GetTranslationVector() ?? Vector3.Zero;

            var delta = new Vector3(vector.X - camera.Target.X, 0f, vector.Z - camera.Target.Z);
            camera.Move(delta);
            camera.Width = Width / Scale;
            camera.Height = Height / Scale;
            camera.SetLens();

            Camera = camera;
            AutoResetCamera = false;
            IsDirty = true;
            Zoom = Scale;
        }

        public override float Scale {
            get { return base.Scale; }
            set {
                if (Equals(value, base.Scale)) return;

                var oldScale = base.Scale;
                base.Scale = value;

                var camera = CameraOrtho;
                if (camera != null && Math.Abs(camera.Width - Width / oldScale) < 0.01) {
                    camera.Width = Width / value;
                    camera.Height = Height / value;
                    camera.SetLens();
                }
            }
        }

        public override void Shot(string outputFile) {
            var width = Width;
            var height = Height;

            base.Shot(outputFile);

            Width = width;
            Height = height;
        }
    }

    public interface ITrackMapRendererFilter {
        bool Filter([CanBeNull] string name);
    }

    public class TrackComplexModelEntry {
        public Kn5 Kn5;
        public Matrix Matrix;
    }

    public class TrackComplexModelDescription {
        private readonly string _modelsIniFilename;
        private List<TrackComplexModelEntry> _models;

        public TrackComplexModelDescription([NotNull] string modelsIniFilename) {
            _modelsIniFilename = modelsIniFilename;
        }

        public static TrackComplexModelDescription CreateLoaded([NotNull] string filename) {
            var d = new TrackComplexModelDescription(filename);
            d.Load();
            return d;
        }

        private IEnumerable<TrackComplexModelEntry> LoadModels() {
            var directory = Path.GetDirectoryName(_modelsIniFilename) ?? "";
            return from section in new IniFile(_modelsIniFilename).GetSections("MODEL")
                   let rot = section.GetSlimVector3("ROTATION")
                   select new TrackComplexModelEntry {
                       Kn5 = Kn5.FromFile(Path.Combine(directory, section.GetNonEmpty("FILE") ?? "")),
                       Matrix = Matrix.Translation(section.GetSlimVector3("POSITION")) * Matrix.RotationYawPitchRoll(rot.X, rot.Y, rot.Z),
                   };
        }

        public void Load() {
            if (_models != null) return;
            _models = LoadModels().ToList();
        }

        internal IEnumerable<TrackComplexModelEntry> GetEntries() {
            Load();
            return _models;
        }
    }

    public class TrackMapRenderer : BaseRenderer {
        public static int OptionMaxSize = 8192;

        [CanBeNull]
        private readonly Kn5 _kn5;

        [CanBeNull]
        private readonly AiLane _aiLane;

        public bool AiLaneMode => _aiLane != null;

        [CanBeNull]
        private readonly TrackComplexModelDescription _description;

        protected override FeatureLevel FeatureLevel => FeatureLevel.Level_10_0;

        public bool UseFxaa = true;
        public float Margin = 10f;
        public int MinSize = 16;
        public int MaxSize = OptionMaxSize;

        public virtual float Scale { get; set; } = 1f;

        public TrackMapRenderer(string mainKn5Filename) : this(Kn5.FromFile(mainKn5Filename)) { }

        public TrackMapRenderer(Kn5 kn5) {
            _kn5 = kn5;
        }

        public TrackMapRenderer(AiLane aiLane) {
            _aiLane = aiLane;
        }

        public TrackMapRenderer(TrackComplexModelDescription description) {
            _description = description;
        }

        //private Kn5MaterialsProvider _materialsProvider;
        private TrackMapInformation _information;

        protected RenderableList RootNode { get; private set; }

        protected RenderableList FilteredNode { get; private set; }

        private int _trianglesCount;

        public int TrianglesCount {
            get { return _trianglesCount; }
            private set {
                if (Equals(value, _trianglesCount)) return;
                _trianglesCount = value;
                OnPropertyChanged();
            }
        }

        private RenderableList Filter(RenderableList source, Func<IRenderableObject, bool> fn) {
            return new RenderableList(source.Name, source.LocalMatrix, source.Where(fn).Select(x => {
                var list = x as RenderableList;
                return list != null ? Filter(list, fn) : x;
            }));
        }

        public class TrackMapInformation {
            public float Width;
            public float Height;

            public float XOffset;
            public float ZOffset;

            public float Margin;
            public float ScaleFactor = 1.0f;
            public float DrawingSize = 10.0f;

            public void SaveTo(string filename) {
                new IniFile {
                    ["PARAMETERS"] = {
                        ["WIDTH"] = Width,
                        ["HEIGHT"] = Height,
                        ["X_OFFSET"] = XOffset,
                        ["Z_OFFSET"] = ZOffset,
                        ["MARGIN"] = Margin,
                        ["SCALE_FACTOR"] = ScaleFactor,
                        ["DRAWING_SIZE"] = DrawingSize
                    }
                }.Save(filename);
            }
        }

        /*protected virtual Kn5MaterialsProvider CreateMaterialsProvider() {
            return new TrackMapMaterialProvider();
        }*/

        private TargetResourceTexture _buffer0, _buffer1;

        [NotNull]
        private static RenderableList ToRenderableList([NotNull] IRenderableObject obj) {
            return obj as RenderableList ?? new RenderableList { obj };
        }

        private bool _aiLaneDirty;
        private bool _aiLaneActualWidth;

        public bool AiLaneActualWidth {
            get { return _aiLaneActualWidth; }
            set {
                if (Equals(value, _aiLaneActualWidth)) return;
                _aiLaneActualWidth = value;
                OnPropertyChanged();
                _aiLaneDirty = true;
                IsDirty = true;
            }
        }

        private float _aiLaneWidth = 10f;

        public float AiLaneWidth {
            get { return _aiLaneWidth; }
            set {
                if (Equals(value, _aiLaneWidth)) return;
                _aiLaneWidth = value;
                OnPropertyChanged();
                _aiLaneDirty = true;
                IsDirty = true;
            }
        }

        private void RebuildAiLane() {
            RootNode.Dispose();
            RootNode = new RenderableList("_root", Matrix.Identity, new[] {
                AiLaneObject.Create(_aiLane, AiLaneActualWidth ? (float?)null : AiLaneWidth)
            });
            UpdateFiltered();
            _aiLaneDirty = false;
        }

        protected override void InitializeInner() {
            DeviceContextHolder.Set<IMaterialsFactory>(new TrackMapMaterialsFactory());

            if (_aiLane != null) {
                RootNode = new RenderableList("_root", Matrix.Identity, new [] {
                    AiLaneObject.Create(_aiLane, AiLaneActualWidth ? (float?)null : AiLaneWidth)
                });
                _aiLaneDirty = false;
            } else if (_kn5 != null) {
                RootNode = ToRenderableList(Convert(_kn5.RootNode));
            } else if (_description != null) {
                RootNode = new RenderableList("_root", Matrix.Identity, _description.GetEntries().Select(x => {
                    var node = ToRenderableList(Convert(x.Kn5.RootNode));
                    node.LocalMatrix = x.Matrix;
                    return node;
                }));
            } else {
                RootNode = new RenderableList();
            }

            _buffer0 = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);
            _buffer1 = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);
        }

        private static IRenderableObject Convert(Kn5Node node) {
            switch (node.NodeClass) {
                case Kn5NodeClass.Base:
                    return new Kn5RenderableList(node, Convert) {
                        IsEnabled = true
                    };

                case Kn5NodeClass.Mesh:
                case Kn5NodeClass.SkinnedMesh:
                    return new Kn5RenderableDepthOnlyObject(node, true) {
                        IsEnabled = true
                    };

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        protected override void ResizeInner() {
            _buffer0.Resize(DeviceContextHolder, Width, Height, null);
            _buffer1.Resize(DeviceContextHolder, Width, Height, null);
            ResetCamera();
        }

        [CanBeNull]
        private ITrackMapRendererFilter _filter;

        protected void UpdateFiltered() {
            if (_aiLane != null) {
                FilteredNode = RootNode;
            } else {
                FilteredNode = Filter(RootNode, n => n is RenderableList ||
                        (_filter?.Filter(n.Name) ?? n.Name?.IndexOf("ROAD", StringComparison.Ordinal) == 1));
            }

            FilteredNode.UpdateBoundingBox();
            TrianglesCount = FilteredNode.BoundingBox.HasValue ? FilteredNode.GetTrianglesCount() : 0;
            IsDirty = true;
        }

        public void SetFilter([CanBeNull] ITrackMapRendererFilter value) {
            _filter = value;
        }

        protected void Prepare() {
            UpdateFiltered();
            if (!FilteredNode.BoundingBox.HasValue) {
                throw new Exception("Can’t find a bounding box for provided filter");
            }

            var box = FilteredNode.BoundingBox.Value;
            var size = box.GetSize();

            {
                var limit = Math.Min(Device.FeatureLevel == FeatureLevel.Level_11_0 ? 16384 : 8192, MaxSize) - Margin * 2;
                var width = size.X * Scale;
                var height = size.Z * Scale;

                if (MinSize > 0) {
                    var min = MinSize / Math.Min(width, height);
                    if (min > 1f) {
                        width *= min;
                        height *= min;
                        Scale *= min;
                    }
                }

                if (limit < int.MaxValue) {
                    var max = limit / Math.Max(width, height);
                    if (max < 1f) {
                        width *= max;
                        height *= max;
                        Scale *= max;
                    }
                }

                Width = (int)(width + Margin * 2);
                Height = (int)(height + Margin * 2);
            }

            _information = new TrackMapInformation {
                Width = Width - Margin * 2,
                Height = Height - Margin * 2,
                Margin = Margin,
                XOffset = -box.Minimum.X + Margin / Scale,
                ZOffset = -box.Minimum.Z + Margin / Scale,
                ScaleFactor = 1 / Scale
            };
        }

        public BaseCamera Camera { get; protected set; }

        protected virtual void ResetCamera() {
            Camera = GetCamera();
            Camera.SetLens(AspectRatio);
        }

        protected virtual CameraOrtho GetCamera() {
            if (!FilteredNode.BoundingBox.HasValue) {
                return new CameraOrtho();
            }

            var box = FilteredNode.BoundingBox.Value;
            return new CameraOrtho {
                Position = new Vector3(box.GetCenter().X, box.Maximum.Y + 1f, box.GetCenter().Z),
                FarZ = box.GetSize().Y + 2f,
                Target = box.GetCenter(),
                Up = new Vector3(0.0f, 0.0f, -1.0f),
                Width = box.GetSize().X + 2 * Margin / Scale,
                Height = box.GetSize().Z + 2 * Margin / Scale
            };
        }

        protected bool ShotMode { get; private set; }

        protected override void DrawOverride() {
            EnsureAiLaneAllRight();
            Camera.UpdateViewMatrix();

            // just in case
            DeviceContext.ClearRenderTargetView(_buffer0.TargetView, Color.Black);
            DeviceContext.ClearRenderTargetView(_buffer1.TargetView, Color.Black);
            DeviceContext.ClearRenderTargetView(RenderTargetView, Color.Transparent);

            // render to buffer-0
            DeviceContext.OutputMerger.SetTargets(_buffer0.TargetView);

            DeviceContext.OutputMerger.BlendState = null;
            DeviceContext.Rasterizer.State = DeviceContextHolder.States.DoubleSidedState;
            FilteredNode.Draw(DeviceContextHolder, Camera, SpecialRenderMode.Simple);
            DeviceContext.Rasterizer.State = null;

            // blur to buffer-0 using buffer-1 as temporary
            DeviceContextHolder.GetHelper<TrackMapBlurRenderHelper>().Blur(DeviceContextHolder, _buffer0, _buffer1);

            // outline map and add inset shadow to buffer-1 (alpha is in green channel for optional FXAA)
            DeviceContextHolder.GetHelper<TrackMapRenderHelper>().Draw(DeviceContextHolder, _buffer0.View, _buffer1.TargetView);

            // move alpha from green channel to alpha-channel
            if (UseFxaa) {
                // applying FXAA first
                DeviceContextHolder.GetHelper<FxaaHelper>().Draw(DeviceContextHolder, _buffer1.View, _buffer0.TargetView);
                DeviceContextHolder.GetHelper<TrackMapRenderHelper>().Final(DeviceContextHolder, _buffer0.View, RenderTargetView, !ShotMode);
            } else {
                // directly
                DeviceContextHolder.GetHelper<TrackMapRenderHelper>().Final(DeviceContextHolder, _buffer1.View, RenderTargetView, !ShotMode);
            }
        }

        protected void EnsureAiLaneAllRight() {
            if (_aiLaneDirty) {
                RebuildAiLane();
            }
        }

        private void SaveResultAs(string filename) {
            using (var stream = new MemoryStream()) { 
                Texture2D.ToStream(DeviceContext, RenderBuffer, ImageFileFormat.Png, stream);
                stream.Position = 0;

                using (var image = Image.FromStream(stream)) {
                    image.Save(filename, ImageFormat.Png);
                }
            }
        }

        public virtual void Shot(string outputFile) {
            ShotMode = true;

            try {
                if (!Initialized) {
                    Initialize();
                }

                EnsureAiLaneAllRight();
                Prepare();
                Draw();
                SaveResultAs(outputFile);
            } finally {
                ShotMode = false;
            }
        }

        public void SaveInformation(string filename) {
            _information.SaveTo(filename);
        }

        protected override void OnTick(float dt) { }

        protected override void DisposeOverride() {
            DisposeHelper.Dispose(ref _buffer0);
            DisposeHelper.Dispose(ref _buffer1);
            // DisposeHelper.Dispose(ref _materialsProvider);
            base.DisposeOverride();
        }
    }

    public class TrackMapMaterialsFactory : IMaterialsFactory {
        public IRenderableMaterial CreateMaterial(object key) {
            if (BasicMaterials.DepthOnlyKey.Equals(key)) {
                return new Kn5MaterialTrackMap();
            }

            return new InvisibleMaterial();
        }
    }

    public class TrackMapRenderHelper : IRenderHelper {
        private EffectSpecialTrackMap _effect;

        public void OnInitialize(DeviceContextHolder holder) {
            _effect = holder.GetEffect<EffectSpecialTrackMap>();
        }

        public void OnResize(DeviceContextHolder holder) { }

        public void Draw(DeviceContextHolder holder, ShaderResourceView view, RenderTargetView target) {
            holder.DeviceContext.OutputMerger.SetTargets(target);
            holder.PrepareQuad(_effect.LayoutPT);
            _effect.FxInputMap.SetResource(view);
            _effect.TechPp.DrawAllPasses(holder.DeviceContext, 6);
        }

        public void Final(DeviceContextHolder holder, ShaderResourceView view, RenderTargetView target, bool checkedBackground) {
            holder.DeviceContext.OutputMerger.SetTargets(target);
            holder.PrepareQuad(_effect.LayoutPT);
            _effect.FxInputMap.SetResource(view);
            (checkedBackground ? _effect.TechFinalCheckers : _effect.TechFinal).DrawAllPasses(holder.DeviceContext, 6);
        }

        public void Dispose() {}
    }

    public class TrackMapBlurRenderHelper : IRenderHelper {
        private EffectSpecialTrackMap _effect;

        public void OnInitialize(DeviceContextHolder holder) {
            _effect = holder.GetEffect<EffectSpecialTrackMap>();
        }

        public void OnResize(DeviceContextHolder holder) {
            _effect.FxScreenSize.Set(new Vector4(holder.Width, holder.Height, 1f / holder.Width, 1f / holder.Height));
        }

        public void Draw(DeviceContextHolder holder, ShaderResourceView view) {
            BlurHorizontally(holder, view);
        }

        public void BlurHorizontally(DeviceContextHolder holder, ShaderResourceView view) {
            holder.DeviceContext.OutputMerger.BlendState = null;
            holder.QuadBuffers.Prepare(holder.DeviceContext, _effect.LayoutPT);
            _effect.FxInputMap.SetResource(view);
            _effect.TechPpHorizontalBlur.DrawAllPasses(holder.DeviceContext, 6);
        }

        public void BlurVertically(DeviceContextHolder holder, ShaderResourceView view) {
            holder.DeviceContext.OutputMerger.BlendState = null;
            holder.QuadBuffers.Prepare(holder.DeviceContext, _effect.LayoutPT);
            _effect.FxInputMap.SetResource(view);
            _effect.TechPpVerticalBlur.DrawAllPasses(holder.DeviceContext, 6);
        }

        public void Blur(DeviceContextHolder holder, TargetResourceTexture source, TargetResourceTexture temporary, int iterations = 1,
                TargetResourceTexture target = null) {
            for (var i = 0; i < iterations; i++) {
                holder.DeviceContext.OutputMerger.SetTargets(temporary.TargetView);
                BlurHorizontally(holder, (i == 0 ? null : target?.View) ?? source.View);
                holder.DeviceContext.OutputMerger.SetTargets(target?.TargetView ?? source.TargetView);
                BlurVertically(holder, temporary.View);
            }
        }

        public void Dispose() { }
    }

    public class Kn5MaterialTrackMap : IRenderableMaterial {
        private EffectSpecialTrackMap _effect;

        internal Kn5MaterialTrackMap() { }

        public void Initialize(IDeviceContextHolder contextHolder) {
            _effect = contextHolder.GetEffect<EffectSpecialTrackMap>();
        }

        public bool Prepare(IDeviceContextHolder contextHolder, SpecialRenderMode mode) {
            if (mode != SpecialRenderMode.Simple) return false;
            contextHolder.DeviceContext.InputAssembler.InputLayout = _effect.LayoutP;
            contextHolder.DeviceContext.OutputMerger.BlendState = null;
            return true;
        }

        public void SetMatrices(Matrix objectTransform, ICamera camera) {
            _effect.FxWorldViewProj.SetMatrix(objectTransform * camera.ViewProj);
        }

        public void Draw(IDeviceContextHolder contextHolder, int indices, SpecialRenderMode mode) {
            _effect.TechMain.DrawAllPasses(contextHolder.DeviceContext, indices);
        }

        public bool IsBlending => false;

        public void Dispose() { }
    }
}