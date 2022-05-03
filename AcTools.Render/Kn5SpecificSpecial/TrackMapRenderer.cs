using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using AcTools.AiFile;
using AcTools.DataFile;
using AcTools.Kn5File;
using AcTools.Numerics;
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

        public TrackMapPreparationRenderer(IKn5 kn5) : base(kn5) {
            Camera = new CameraOrtho();
        }

        public TrackMapPreparationRenderer(AiSpline aiSpline, AiSpline aiPitsSpline, string dataDir) : base(aiSpline, aiPitsSpline, dataDir) {
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
            get => _zoom;
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

        public void SelectPreviousSkin() { }
        public void SelectNextSkin() { }
        public void SelectSkin(string skinId) { }

        void IKn5ObjectRenderer.ResetCamera() {
            ResetCamera();
        }

        public void ChangeCameraFov(float newFovY) { }
        public void RefreshMaterial(IKn5 kn5, uint materialId) { }
        public void UpdateMaterialPropertyA(IKn5 kn5, uint materialId, string propertyName, float valueA) { }
        public void UpdateMaterialPropertyC(IKn5 kn5, uint materialId, string propertyName, Vec3 valueC) { }

        protected sealed override void DrawSprites() {
            var sprite = Sprite;
            if (sprite == null || ShotMode) return;
            DrawSpritesInner();
            sprite.Flush();
        }

        protected void DrawSpritesInner() {
            if (!VisibleUi) return;

            if (_textBlock == null) {
                _textBlock = new TextBlockRenderer(Sprite, "Arial", FontWeight.Normal, FontStyle.Normal, FontStretch.Normal, 20f);
            }

            if (TrianglesCount == 0) {
                _textBlock.DrawString("Nothing found",
                        new RectangleF(0, 0, Width, Height), 0f, TextAlignment.VerticalCenter | TextAlignment.HorizontalCenter, 20f,
                        new Color4(1.0f, 1.0f, 1.0f), CoordinateType.Absolute);
            } else {
                _textBlock.DrawString($"Triangles: {TrianglesCount}\nZoom: {Zoom:F3}×\nResult image most likely will be different size",
                        new RectangleF(8, 8, Width - 16, Height - 16), 0f, TextAlignment.Bottom | TextAlignment.Left, 12f,
                        new Color4(1.0f, 1.0f, 1.0f), CoordinateType.Absolute);
            }
        }

        protected override void ResetCamera() {
            AutoResetCamera = true;
            base.ResetCamera();
            IsDirty = true;

            var camera = CameraOrtho;
            Zoom = camera == null ? 1f : Math.Min(Width / camera.Width, Height / camera.Height);
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
            get => base.Scale;
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
        public IKn5 Kn5;
        public Matrix Matrix;
    }

    public class TrackComplexModelDescription {
        private readonly string _modelsIniFilename;
        private List<TrackComplexModelEntry> _models;

        public IKn5TextureLoader TextureLoader { get; set; } = DefaultKn5TextureLoader.Instance;
        public IKn5NodeLoader NodeLoader { get; set; } = DefaultKn5NodeLoader.Instance;
        public IKn5MaterialLoader MaterialLoader { get; set; } = DefaultKn5MaterialLoader.Instance;

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
                    Kn5 = Kn5.FromFile(Path.Combine(directory, section.GetNonEmpty("FILE") ?? ""), TextureLoader, MaterialLoader, NodeLoader),
                    Matrix = Matrix.Translation(section.GetSlimVector3("POSITION")) * Matrix.RotationYawPitchRoll(rot.X, rot.Y, rot.Z),
                };
        }

        private void Load() {
            if (_models != null) return;
            _models = LoadModels().ToList();
        }

        public IEnumerable<TrackComplexModelEntry> GetEntries() {
            Load();
            return _models;
        }
    }

    public class TrackMapRenderer : BaseRenderer {
        public static int OptionMaxSize = 8192;

        [CanBeNull]
        private readonly IKn5 _kn5;

        [CanBeNull]
        private readonly AiSpline _aiSpline;

        [CanBeNull]
        private readonly AiSpline _aiPitsSpline;

        public bool AiLaneMode => _aiSpline != null;

        [CanBeNull]
        private readonly TrackComplexModelDescription _description;

        protected override FeatureLevel FeatureLevel => FeatureLevel.Level_10_0;

        public bool UseFxaa = true;
        public float Margin = 10f;
        public int MinSize = 16;
        public int MaxSize = OptionMaxSize;

        public virtual float Scale { get; set; } = 1f;

        public TrackMapRenderer(string mainKn5Filename) : this(Kn5.FromFile(mainKn5Filename)) { }

        public TrackMapRenderer(IKn5 kn5) {
            _kn5 = kn5;
        }

        public TrackMapRenderer(AiSpline aiSpline, [CanBeNull] AiSpline aiPitsSpline, [CanBeNull] string dataDir) {
            _aiSpline = aiSpline;
            _aiPitsSpline = aiPitsSpline;

            if (dataDir != null) {
                _extraMarks = new List<Tuple<double, Vector3>>();

                var sectors = new List<Tuple<double, double>>();
                foreach (var section in new IniFile(Path.Combine(dataDir, "sections.ini")).GetSections("SECTION")) {
                    var valueIn = section.GetDoubleNullable("IN");
                    var valueOut = section.GetDoubleNullable("OUT");
                    if (valueIn.HasValue && valueOut.HasValue) {
                        sectors.Add(Tuple.Create(valueIn.Value, valueOut.Value));
                    } else {
                        sectors = null;
                        break;
                    }
                    // if (valueIn.HasValue) _extraMarks.Add(Tuple.Create(valueIn.Value, new Vector3(0.45f)));
                    // if (valueOut.HasValue) _extraMarks.Add(Tuple.Create(valueOut.Value, new Vector3(0.45f)));
                }
                if (sectors?.Count > 0) {
                    if (sectors.Count == 1) {
                        _extraMarks.Add(Tuple.Create(sectors[0].Item1, new Vector3(0.4f)));
                        _extraMarks.Add(Tuple.Create(sectors[1].Item2, new Vector3(0.4f)));
                    } else {
                        double MixProgress(double v1, double v2) {
                            if (v2 < v1) {
                                var v = v1;
                                v1 = v2;
                                v2 = v;
                            }
                            if (v2 - v1 > 0.5) {
                                return (v2 + v1 + 1) / 2 % 1d;
                            }
                            return (v1 + v2) / 2;
                        }
                        for (var i = 0; i < sectors.Count; ++i) {
                            _extraMarks.Add(Tuple.Create(MixProgress(sectors[i].Item2, sectors[(i + 1) % sectors.Count].Item1), new Vector3(0.45f)));
                        }
                    }
                }

                foreach (var section in new IniFile(Path.Combine(dataDir, "drs_zones.ini")).GetSections("ZONE")) {
                    var valueIn = section.GetDoubleNullable("START");
                    var valueOut = section.GetDoubleNullable("END");
                    var valueSpecial = section.GetDoubleNullable("DETECTION");
                    if (valueIn.HasValue) _extraMarks.Add(Tuple.Create(valueIn.Value, new Vector3(0.95f, 0.85f, 0.04f)));
                    if (valueOut.HasValue) _extraMarks.Add(Tuple.Create(valueOut.Value, new Vector3(0.95f, 0.85f, 0.04f)));
                    if (valueSpecial.HasValue) _extraMarks.Add(Tuple.Create(valueSpecial.Value, new Vector3(0.04f, 0.95f, 0.36f)));
                }
            }
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
            get => _trianglesCount;
            private set {
                if (Equals(value, _trianglesCount)) return;
                _trianglesCount = value;
                OnPropertyChanged();
            }
        }

        private RenderableList Filter(RenderableList source, Func<IRenderableObject, bool> fn) {
            return new RenderableList(source.Name, source.LocalMatrix, source.Where(fn).Select(x => x is RenderableList list ? Filter(list, fn) : x));
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
                FileUtils.EnsureFileDirectoryExists(filename);
                new IniFile {
                    ["PARAMETERS"] = {
                        ["WIDTH"] = Width + Margin * 2,
                        ["HEIGHT"] = Height + Margin * 2,
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

        private TargetResourceTexture _buffer0, _buffer1, _buffer2;

        [NotNull]
        private static RenderableList ToRenderableList([NotNull] IRenderableObject obj) {
            return obj as RenderableList ?? new RenderableList { obj };
        }

        private bool _aiLaneDirty;
        private bool _aiLaneActualWidth;

        public bool AiLaneActualWidth {
            get => _aiLaneActualWidth;
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
            get => _aiLaneWidth;
            set {
                if (Equals(value, _aiLaneWidth)) return;
                _aiLaneWidth = value;
                OnPropertyChanged();
                _aiLaneDirty = true;
                IsDirty = true;
            }
        }

        private bool _showPitlane;

        public bool ShowPitlane {
            get => _showPitlane;
            set {
                if (Equals(value, _showPitlane)) return;
                _showPitlane = value;
                OnPropertyChanged();
                _aiLaneDirty = true;
                IsDirty = true;
            }
        }

        private bool _showSpecialMarks;

        public bool ShowSpecialMarks {
            get => _showSpecialMarks;
            set {
                if (Equals(value, _showSpecialMarks)) return;
                _showSpecialMarks = value;
                OnPropertyChanged();
                _aiLaneDirty = true;
                IsDirty = true;
            }
        }

        private List<Tuple<double, Vector3>> _extraMarks;

        private RenderableList CreateAiLaneList() {
            var width = AiLaneActualWidth ? (float?)null : AiLaneWidth;
            var items = new[] {
                ShowPitlane && _aiPitsSpline != null ? AiLaneObject.Create(_aiPitsSpline, width, "pits") : null,
                _aiSpline != null ? AiLaneObject.Create(_aiSpline, width, "main") : null,
                ShowSpecialMarks && _aiSpline != null ? AiLaneObject.Create(_aiSpline, width, 6f, 1f, new Vector3(0.80f, 0.04f, 0.95f)) : null,
            }.NonNull();
            if (ShowSpecialMarks && _extraMarks != null && _aiSpline != null) {
                items = items.Concat(_extraMarks.Select(x => AiLaneObject.Create(_aiSpline, width, 3f, (float)x.Item1, x.Item2)));
            }
            return new RenderableList("_root", Matrix.Identity, items);
        }

        private void RebuildAiLane() {
            if (_aiSpline == null) return;
            RootNode.Dispose();
            RootNode = CreateAiLaneList();
            UpdateFiltered();
            _aiLaneDirty = false;
        }

        protected override void InitializeInner() {
            DeviceContextHolder.Set<IMaterialsFactory>(new TrackMapMaterialsFactory());

            if (_aiSpline != null) {
                RootNode = CreateAiLaneList();
                _aiLaneDirty = false;
            } else if (_kn5 != null) {
                RootNode = ToRenderableList(Kn5DepthOnlyForceVisibleConverter.Instance.Convert(_kn5.RootNode));
            } else if (_description != null) {
                RootNode = new RenderableList("_root", Matrix.Identity, _description.GetEntries().Select(x => {
                    var node = ToRenderableList(Kn5DepthOnlyForceVisibleConverter.Instance.Convert(x.Kn5.RootNode));
                    node.LocalMatrix = x.Matrix;
                    return node;
                }));
            } else {
                RootNode = new RenderableList();
            }

            _buffer0 = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);
            _buffer1 = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);
            _buffer2 = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);
        }

        protected override void ResizeInner() {
            _buffer0.Resize(DeviceContextHolder, Width, Height, null);
            _buffer1.Resize(DeviceContextHolder, Width, Height, null);
            _buffer2.Resize(DeviceContextHolder, Width, Height, null);
            ResetCamera();
        }

        [CanBeNull]
        private ITrackMapRendererFilter _filter;

        protected void UpdateFiltered() {
            if (_aiSpline != null) {
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

        public CameraBase Camera { get; protected set; }

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
            EnsureAiLaneIsAllRight();
            Camera.UpdateViewMatrix();

            // just in case
            DeviceContext.ClearRenderTargetView(_buffer0.TargetView, Color.Transparent);
            DeviceContext.ClearRenderTargetView(_buffer1.TargetView, Color.Transparent);
            DeviceContext.ClearRenderTargetView(_buffer2.TargetView, Color.Transparent);
            DeviceContext.ClearRenderTargetView(RenderTargetView, Color.Transparent);

            // render to buffer-0
            DeviceContext.OutputMerger.SetTargets(_buffer0.TargetView);

            DeviceContext.OutputMerger.BlendState = null;
            DeviceContext.Rasterizer.State = DeviceContextHolder.States.DoubleSidedState;
            FilteredNode.Draw(DeviceContextHolder, Camera, SpecialRenderMode.Simple);
            DeviceContext.Rasterizer.State = null;

            // blur to buffer-0 using buffer-1 as temporary
            DeviceContextHolder.GetHelper<TrackMapBlurRenderHelper>().Blur(DeviceContextHolder, _buffer0, _buffer1, 1, _buffer2);

            // outline map and add inset shadow to buffer-1 (alpha is in green channel for optional FXAA)
            DeviceContextHolder.GetHelper<TrackMapRenderHelper>().Draw(DeviceContextHolder, _buffer0.View, _buffer2.View, _buffer1.TargetView);

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

        protected void EnsureAiLaneIsAllRight() {
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

                EnsureAiLaneIsAllRight();
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

        protected override void OnTickOverride(float dt) { }

        protected override void DisposeOverride() {
            DisposeHelper.Dispose(ref _buffer0);
            DisposeHelper.Dispose(ref _buffer1);
            // DisposeHelper.Dispose(ref _materialsProvider);
            base.DisposeOverride();
        }
    }
}