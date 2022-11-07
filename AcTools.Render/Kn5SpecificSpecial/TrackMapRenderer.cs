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

                /*var sectors = new List<Tuple<double, double>>();
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
                }*/

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

        protected RenderableList FilteredBaseNode { get; private set; }

        protected RenderableList MarksNode { get; private set; }

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

        private class Layer : IDisposable {
            public TargetResourceTexture[] Buffers;

            public Layer() {
                Buffers = new[] {
                    TargetResourceTexture.Create(Format.R8G8B8A8_UNorm),
                    TargetResourceTexture.Create(Format.R8G8B8A8_UNorm),
                    TargetResourceTexture.Create(Format.R8G8B8A8_UNorm),
                };
            }

            public void Dispose() {
                Buffers.DisposeEverything();
            }

            public void Resize(DeviceContextHolder contextHolder, int width, int height) {
                foreach (var buffer in Buffers) {
                    buffer.Resize(contextHolder, width, height, null);
                }
            }

            public void Clear(DeviceContext context) {
                foreach (var buffer in Buffers) {
                    context.ClearRenderTargetView(buffer.TargetView, Color.Transparent);
                }
            }

            public void Draw(DeviceContextHolder contextHolder, ICamera camera, RenderableList node) {
                var context = contextHolder.DeviceContext;
                foreach (var buffer in Buffers) {
                    context.ClearRenderTargetView(buffer.TargetView, Color.Transparent);
                }

                context.OutputMerger.SetTargets(Buffers[0].TargetView);

                context.OutputMerger.BlendState = null;
                context.Rasterizer.State = contextHolder.States.DoubleSidedState;
                node.Draw(contextHolder, camera, SpecialRenderMode.Simple);
                contextHolder.DeviceContext.Rasterizer.State = null;

                // blur to buffer-0 using buffer-1 as temporary
                contextHolder.GetHelper<TrackMapBlurRenderHelper>().Blur(contextHolder, Buffers[0], Buffers[1], 1, Buffers[2]);

                // outline map and add inset shadow to buffer-1 (alpha is in green channel for optional FXAA)
                contextHolder.GetHelper<TrackMapRenderHelper>().Draw(contextHolder, Buffers[0].View, Buffers[2].View, Buffers[1].TargetView);
            }
        }

        private Layer _layerBase, _layerMarks;

        [NotNull]
        private static RenderableList ToRenderableList([NotNull] IRenderableObject obj) {
            return obj as RenderableList ?? new RenderableList { obj };
        }

        private bool _aiLaneBaseDirty;
        private bool _aiLaneMarksDirty;
        private bool _aiLaneActualWidth;

        public bool AiLaneActualWidth {
            get => _aiLaneActualWidth;
            set {
                if (Equals(value, _aiLaneActualWidth)) return;
                _aiLaneActualWidth = value;
                OnPropertyChanged();
                _aiLaneBaseDirty = true;
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
                _aiLaneBaseDirty = true;
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
                _aiLaneBaseDirty = true;
                IsDirty = true;
            }
        }

        private float _aiPitLaneWidth = 6f;

        public float AiPitLaneWidth {
            get => _aiPitLaneWidth;
            set {
                if (Equals(value, _aiPitLaneWidth)) return;
                _aiPitLaneWidth = value;
                OnPropertyChanged();
                _aiLaneBaseDirty = true;
                IsDirty = true;
            }
        }

        private Vector3 _aiPitLaneColor = new Vector3(.3f, .3f, .3f);

        public Vector3 AiPitLaneColor {
            get => _aiPitLaneColor;
            set {
                if (Equals(value, _aiPitLaneColor)) return;
                _aiPitLaneColor = value;
                OnPropertyChanged();
                _aiLaneBaseDirty = true;
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
                _aiLaneMarksDirty = true;
                IsDirty = true;
            }
        }

        private bool _showAiPitLaneMarks;

        public bool ShowAiPitLaneMarks {
            get => _showAiPitLaneMarks;
            set {
                if (Equals(value, _showAiPitLaneMarks)) return;
                _showAiPitLaneMarks = value;
                OnPropertyChanged();
                _aiLaneBaseDirty = true;
                IsDirty = true;
            }
        }

        private float _specialMarksWidth;

        public float SpecialMarksWidth {
            get => _specialMarksWidth;
            set {
                if (Equals(value, _specialMarksWidth)) return;
                _specialMarksWidth = value;
                OnPropertyChanged();
                _aiLaneMarksDirty = true;
                IsDirty = true;
            }
        }

        private float _specialMarksThickness;

        public float SpecialMarksThickness {
            get => _specialMarksThickness;
            set {
                if (Equals(value, _specialMarksThickness)) return;
                _specialMarksThickness = value;
                OnPropertyChanged();
                _aiLaneMarksDirty = true;
                IsDirty = true;
            }
        }

        private List<Tuple<double, Vector3>> _extraMarks;

        private void RebuildAiLane() {
            if (_aiSpline == null) return;
            RootNode.Dispose();
            RootNode = CreateAiLaneBaseList();
            UpdateFiltered();
            _aiLaneBaseDirty = false;
        }

        private void RebuildAiMarks() {
            if (_aiSpline == null) return;
            MarksNode.Dispose();
            MarksNode = CreateAiMarksList();
            UpdateFiltered();
            _aiLaneMarksDirty = false;
        }

        private Tuple<float, float> _pitLaneMarks;

        private Tuple<float, float> GetPitLaneMarks() {
            if (_pitLaneMarks == null) {
                var entry = -1f;
                var exit = -1f;
                if (_aiSpline != null && _aiPitsSpline != null) {
                    var close = true;
                    var lastJ = -1;
                    for (var i = 1; i < 20; ++i) {
                        var p = i / 20f;
                        var o = _aiPitsSpline.Points[(int)(p * _aiPitsSpline.Points.Length)];
                        // AcToolsLogging.Write($"i={i}, p={o.Position}");

                        var j = 0;
                        for (; j < _aiSpline.Points.Length; ++j) {
                            if ((o.Position - _aiSpline.Points[j].Position).LengthSquared() < 6f) {
                                // AcToolsLogging.Write($"j={j}, jp={_aiSpline.Points[j].Position}, l={(o.Position - _aiSpline.Points[j].Position).Length()}");
                                lastJ = j;
                                break;
                            }
                        }

                        // AcToolsLogging.Write($"j={j}, close={close}");
                        if (j < _aiSpline.Points.Length != close) {
                            // AcToolsLogging.Write($"adding mark: j={j}, lastJ={lastJ}, out of {_aiSpline.Points.Length}");
                            if (close) {
                                entry = (float)lastJ / _aiSpline.Points.Length;
                            } else {
                                exit = (float)j / _aiSpline.Points.Length;
                            }
                            if (!close) {
                                break;
                            }
                            close = false;
                        }
                    }
                }
                _pitLaneMarks = Tuple.Create(entry, exit);
            }
            return _pitLaneMarks;
        }

        private RenderableList CreateAiLaneBaseList() {
            var width = AiLaneActualWidth ? (float?)null : AiLaneWidth;

            var list = new List<IRenderableObject> {
                ShowPitlane && _aiPitsSpline != null ? AiLaneObject.Create(_aiPitsSpline, AiPitLaneWidth, AiPitLaneColor) : null,
                _aiSpline != null ? AiLaneObject.Create(_aiSpline, width, "main") : null,
            };

            if (_aiSpline != null && _aiPitsSpline != null && ShowAiPitLaneMarks) {
                var marks = GetPitLaneMarks();
                try {
                    if (marks.Item1 >= 0d) {
                        list.Add(AiLaneObject.Create(_aiSpline, width, 0f, 4f, marks.Item1, AiPitLaneColor));
                    }
                    if (marks.Item2 >= 0d) {
                        list.Add(AiLaneObject.Create(_aiSpline, width, 0f, 4f, marks.Item2, AiPitLaneColor));
                    }
                } catch (Exception e) {
                    AcToolsLogging.Write(e.Message);
                    return null;
                }
            }

            return new RenderableList("_root", Matrix.Identity, list.NonNull());
        }

        private RenderableList CreateAiMarksList() {
            var list = new List<IRenderableObject>();
            if (ShowSpecialMarks && _aiSpline != null) {
                var width = AiLaneActualWidth ? (float?)null : AiLaneWidth;

                if (_extraMarks != null) {
                    list.AddRange(_extraMarks.Select(x => {
                        try {
                            return AiLaneObject.Create(_aiSpline, width, SpecialMarksWidth * 0.6f, SpecialMarksThickness, (float)x.Item1, x.Item2);
                        } catch (Exception e) {
                            AcToolsLogging.Write(e.Message);
                            return null;
                        }
                    }).NonNull());
                }

                try {
                    list.Add(AiLaneObject.Create(_aiSpline, width, SpecialMarksWidth, SpecialMarksThickness, 1f, new Vector3(0.80f, 0.04f, 0.95f)));
                } catch (Exception e) {
                    AcToolsLogging.Write(e.Message);
                    return null;
                }
            }
            return new RenderableList("_root", Matrix.Identity, list);
        }

        protected override void InitializeInner() {
            DeviceContextHolder.Set<IMaterialsFactory>(new TrackMapMaterialsFactory());

            if (_aiSpline != null) {
                RootNode = CreateAiLaneBaseList();
                MarksNode = CreateAiMarksList();
                _aiLaneBaseDirty = false;
                _aiLaneMarksDirty = false;
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

            _layerBase = new Layer();
            _layerMarks = new Layer();
        }

        protected override void ResizeInner() {
            _layerBase.Resize(DeviceContextHolder, Width, Height);
            _layerMarks.Resize(DeviceContextHolder, Width, Height);
            ResetCamera();
        }

        [CanBeNull]
        private ITrackMapRendererFilter _filter;

        protected void UpdateFiltered() {
            if (_aiSpline != null) {
                FilteredBaseNode = RootNode;
            } else {
                FilteredBaseNode = Filter(RootNode, n => n is RenderableList ||
                        (_filter?.Filter(n.Name) ?? n.Name?.IndexOf("ROAD", StringComparison.Ordinal) == 1));
            }

            FilteredBaseNode.UpdateBoundingBox();
            TrianglesCount = FilteredBaseNode.BoundingBox.HasValue ? FilteredBaseNode.GetTrianglesCount() : 0;
            IsDirty = true;
        }

        public void SetFilter([CanBeNull] ITrackMapRendererFilter value) {
            _filter = value;
        }

        protected void Prepare() {
            UpdateFiltered();
            if (!FilteredBaseNode.BoundingBox.HasValue) {
                throw new Exception("Can’t find a bounding box for provided filter");
            }

            var box = FilteredBaseNode.BoundingBox.Value;
            if (MarksNode?.BoundingBox != null) {
                box = box.ExtendBy(MarksNode.BoundingBox.Value);
            }

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
            if (!FilteredBaseNode.BoundingBox.HasValue) {
                return new CameraOrtho();
            }

            var box = FilteredBaseNode.BoundingBox.Value;
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

            _layerBase.Draw(DeviceContextHolder, Camera, FilteredBaseNode);
            if (MarksNode != null) {
                _layerMarks.Draw(DeviceContextHolder, Camera, MarksNode);
            } else {
                _layerMarks.Clear(DeviceContext);
            }
            DeviceContext.ClearRenderTargetView(RenderTargetView, Color.Transparent);

            // move alpha from green channel to alpha-channel
            if (UseFxaa) {
                // applying FXAA first
                DeviceContextHolder.GetHelper<FxaaHelper>().Draw(DeviceContextHolder, _layerBase.Buffers[1].View, _layerBase.Buffers[0].TargetView);
                DeviceContextHolder.GetHelper<FxaaHelper>().Draw(DeviceContextHolder, _layerMarks.Buffers[1].View, _layerMarks.Buffers[0].TargetView);
                DeviceContextHolder.GetHelper<TrackMapRenderHelper>().Final(DeviceContextHolder,
                        _layerBase.Buffers[0].View, _layerMarks.Buffers[0].View, RenderTargetView, !ShotMode);
            } else {
                // directly
                DeviceContextHolder.GetHelper<TrackMapRenderHelper>().Final(DeviceContextHolder,
                        _layerBase.Buffers[1].View, _layerMarks.Buffers[1].View, RenderTargetView, !ShotMode);
            }
        }

        protected void EnsureAiLaneIsAllRight() {
            if (_aiLaneBaseDirty) {
                RebuildAiLane();
            }
            if (_aiLaneMarksDirty) {
                RebuildAiMarks();
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
            DisposeHelper.Dispose(ref _layerBase);
            DisposeHelper.Dispose(ref _layerMarks);
            // DisposeHelper.Dispose(ref _materialsProvider);
            base.DisposeOverride();
        }
    }
}