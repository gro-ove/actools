using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using AcTools.Kn5File;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shaders;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Base.Utils;
using AcTools.Render.Forward;
using AcTools.Render.Kn5Specific;
using AcTools.Render.Kn5Specific.Materials;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5Specific.Textures;
using AcTools.Render.Kn5Specific.Utils;
using AcTools.Render.Kn5SpecificForward.Materials;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DirectWrite;
using SlimDX.DXGI;
using SpriteTextRenderer;
using FontStyle = SlimDX.DirectWrite.FontStyle;
using TextAlignment = SpriteTextRenderer.TextAlignment;
using TextBlockRenderer = SpriteTextRenderer.SlimDX.TextBlockRenderer;

namespace AcTools.Render.Kn5SpecificForward {
    public class ForwardKn5ObjectRenderer : ForwardRenderer, IKn5ObjectRenderer {
        public CameraOrbit CameraOrbit => Camera as CameraOrbit;

        public FpsCamera FpsCamera => Camera as FpsCamera;

        public bool AutoRotate { get; set; } = true;

        public bool AutoAdjustTarget { get; set; } = true;

        public bool VisibleUi { get; set; } = true;

        private bool _liveReload = true;

        public bool LiveReload {
            get { return _liveReload; }
            set {
                if (Equals(value, _liveReload)) return;
                _liveReload = value;
                OnPropertyChanged();

                _carHelper.LiveReload = value;
            }
        }

        private bool _useFpsCamera;

        public bool UseFpsCamera {
            get { return _useFpsCamera; }
            set {
                if (Equals(value, _useFpsCamera)) return;
                _useFpsCamera = value;
                OnPropertyChanged();

                if (value) {
                    var orbit = CameraOrbit ?? CreateCamera(Scene);
                    Camera = new FpsCamera(orbit.FovY) {
                        NearZ = orbit.NearZ,
                        FarZ = orbit.FarZ
                    };

                    Camera.LookAt(orbit.Position, orbit.Target, orbit.Up);
                } else {
                    Camera = _resetCamera.Clone();
                }

                Camera.SetLens(AspectRatio);
            }
        }

        public Kn5 Kn5 { get; }

        private readonly CarHelper _carHelper;

        public ForwardKn5ObjectRenderer(string mainKn5Filename, string carDirectory = null) {
            Kn5 = Kn5.FromFile(mainKn5Filename);
            _carHelper = new CarHelper(Kn5, carDirectory);
        }

        public string CurrentSkin => _carHelper.CurrentSkin;

        public void SelectPreviousSkin() {
            if (_materialsProvider == null) return;
            _carHelper.SelectPreviousSkin(DeviceContextHolder);
            IsDirty = true;
            OnPropertyChanged(nameof(CurrentSkin));
        }

        public void SelectNextSkin() {
            if (_materialsProvider == null) return;
            _carHelper.SelectNextSkin(DeviceContextHolder);
            IsDirty = true;
            OnPropertyChanged(nameof(CurrentSkin));
        }

        public void SelectSkin(string skinId) {
            if (_materialsProvider == null) {
                _selectSkin = skinId;
                return;
            }

            _carHelper.SelectSkin(skinId, DeviceContextHolder);
            IsDirty = true;
            OnPropertyChanged(nameof(CurrentSkin));
        }

        private string _selectSkin;
        private Kn5MaterialsProvider _materialsProvider;
        private TexturesProvider _texturesProvider;

        [CanBeNull]
        private List<CarLight> _carLights;

        protected override void InitializeInner() {
            base.InitializeInner();

            _materialsProvider = new MaterialsProviderSimple();
            _texturesProvider = new TexturesProvider();
            DeviceContextHolder.Set(_materialsProvider);
            DeviceContextHolder.Set(_texturesProvider);
            
            _carHelper.SetKn5(DeviceContextHolder);
            _carHelper.SkinTextureUpdated += (sender, args) => IsDirty = true;

            var node = Kn5Converter.Convert(Kn5.RootNode, DeviceContextHolder);
            Scene.Add(node);

            var asList = node as Kn5RenderableList;
            if (asList != null) {
                Scene.InsertRange(0, _carHelper.LoadAmbientShadows(asList, 0f));
                _carHelper.AdjustPosition(asList);
                _carHelper.LoadMirrors(asList, DeviceContextHolder);

                _carLights = _carHelper.LoadLights(asList).ToList();
            }

            Scene.UpdateBoundingBox();
            TrianglesCount = node.TrianglesCount;
            ObjectsCount = node.ObjectsCount;

            Camera = CreateCamera(node);
            _resetCamera = (CameraOrbit)Camera.Clone();

            if (_selectSkin != null) {
                SelectSkin(_selectSkin);
                _selectSkin = null;
            }
        }

        private static CameraOrbit CreateCamera(IRenderableObject node) {
            return new CameraOrbit(MathF.ToRadians(32f)) {
                Alpha = 0.9f,
                Beta = 0.1f,
                NearZ = 0.01f,
                FarZ = 200f,
                Radius = node?.BoundingBox?.GetSize().Length() ?? 4.8f,
                Target = (node?.BoundingBox?.GetCenter() ?? Vector3.Zero) - new Vector3(0f, 0.05f, 0f)
            };
        }

        private float _resetState;
        private CameraOrbit _resetCamera;

        public void ResetCamera() {
            UseFpsCamera = false;
            AutoRotate = true;
            _resetState = 1f;
        }

        public bool CarLightsEnabled {
            get { return _carLights?.FirstOrDefault()?.IsEnabled == true; }
            set {
                if (_carLights == null) return;
                foreach (var light in _carLights) {
                    if (light.IsEnabled == value) return;
                    light.IsEnabled = value;
                }
                IsDirty = true;
                OnPropertyChanged(nameof(CarLightsEnabled));
            }
        }

        private TargetResourceTexture _outlineBuffer;
        private TargetResourceDepthTexture _outlineDepthBuffer;

        protected override void ResizeInner() {
            base.ResizeInner();
            _outlineBuffer?.Resize(DeviceContextHolder, Width, Height);
            _outlineDepthBuffer?.Resize(DeviceContextHolder, Width, Height);
        }

        protected override void DrawPrepare() {
            base.DrawPrepare();
            DeviceContextHolder.GetEffect<EffectSimpleMaterial>().FxEyePosW.Set(ActualCamera.Position);

            if (SelectedObject == null || _outlineBuffer == null) return;
            DeviceContext.ClearDepthStencilView(_outlineDepthBuffer.DepthView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1f, 0);
            DeviceContext.OutputMerger.SetTargets(_outlineDepthBuffer.DepthView);
            DeviceContext.Rasterizer.State = DeviceContextHolder.DoubleSidedState;
            SelectedObject.Draw(DeviceContextHolder, ActualCamera, SpecialRenderMode.Simple | SpecialRenderMode.SimpleTransparent);

            DeviceContext.ClearRenderTargetView(_outlineBuffer.TargetView, Color.Transparent);
            DeviceContext.OutputMerger.SetTargets(_outlineBuffer.TargetView);

            var effect = DeviceContextHolder.GetEffect<EffectPpOutline>();
            effect.FxDepthMap.SetResource(_outlineDepthBuffer.View);
            effect.FxScreenSize.Set(new Vector4(Width, Height, 1f / Width, 1f / Height));
            DeviceContextHolder.PrepareQuad(effect.LayoutPT);
            effect.TechOutline.DrawAllPasses(DeviceContext, 6);
        }

        protected override void DrawAfter() {
            base.DrawAfter();

            if (SelectedObject == null || _outlineBuffer == null) return;

            var effect = DeviceContextHolder.GetEffect<EffectPpBasic>();
            DeviceContext.OutputMerger.BlendState = DeviceContextHolder.TransparentBlendState;
            DeviceContextHolder.PrepareQuad(effect.LayoutPT);
            effect.FxInputMap.SetResource(_outlineBuffer.View);

            DeviceContext.Rasterizer.State = null;
            DeviceContext.OutputMerger.DepthStencilState = DeviceContextHolder.DisabledDepthState;
            effect.TechCopy.DrawAllPasses(DeviceContext, 6);
        }

        private TextBlockRenderer _textBlock;

        protected override void DrawSpritesInner() {
            if (!VisibleUi) return;

            if (_textBlock == null) {
                _textBlock = new TextBlockRenderer(Sprite, "Arial", FontWeight.Normal, FontStyle.Normal, FontStretch.Normal, 24f);
            }

            _textBlock.DrawString($@"
FPS: {FramesPerSecond:F0}{(SyncInterval ? " (limited)" : "")}
FXAA: {(!UseFxaa ? "No" : "Yes")}
Bloom: {(!UseBloom ? "No" : "Yes")}
Triangles: {TrianglesCount:D}".Trim(),
                    new Vector2(Width - 300, 20), 16f, new Color4(1.0f, 1.0f, 1.0f),
                    CoordinateType.Absolute);

            if (_carHelper.Skins != null && _carHelper.CurrentSkin != null) {
                _textBlock.DrawString($"{_carHelper.CurrentSkin} ({_carHelper.Skins.IndexOf(_carHelper.CurrentSkin) + 1}/{_carHelper.Skins.Count})", new RectangleF(0f, 0f, Width, Height - 20),
                        TextAlignment.HorizontalCenter | TextAlignment.Bottom, 16f, new Color4(1.0f, 1.0f, 1.0f),
                        CoordinateType.Absolute);
            }
        }

        private float _elapsedCamera;

        protected override void OnTick(float dt) {
            base.OnTick(dt);

            const float threshold = 0.001f;
            if (_resetState > threshold) {
                if (!AutoRotate) {
                    _resetState = 0f;
                    return;
                }

                AutoAdjustTarget = true;

                _resetState += (-0f - _resetState) / 10f;
                if (_resetState <= threshold) {
                    AutoRotate = false;
                }

                var cam = CameraOrbit;
                if (cam != null) {
                    cam.Alpha += (_resetCamera.Alpha - cam.Alpha) / 10f;
                    cam.Beta += (_resetCamera.Beta - cam.Beta) / 10f;
                    cam.Radius += (_resetCamera.Radius - cam.Radius) / 10f;
                    cam.FovY += (_resetCamera.FovY - cam.FovY) / 10f;
                }

                _elapsedCamera = 0f;

                IsDirty = true;
            } else if (AutoRotate && CameraOrbit != null) {
                CameraOrbit.Alpha += dt * 0.29f;
                CameraOrbit.Beta += (MathF.Sin(_elapsedCamera * 0.39f) * 0.2f + 0.15f - CameraOrbit.Beta) / 10f;
                _elapsedCamera += dt;

                IsDirty = true;
            }

            if (AutoAdjustTarget && CameraOrbit != null) {
                var t = _resetCamera.Target + new Vector3(-0.2f * CameraOrbit.Position.X, -0.1f * CameraOrbit.Position.Y, 0f);
                CameraOrbit.Target += (t - CameraOrbit.Target) / 2f;
            }
        }

        public async Task UpdateTextureAsync(string filename) {
            await _texturesProvider.UpdateTextureAsync(filename, DeviceContextHolder);
            IsDirty = true;
        }

        private string _selectedName;

        public string SelectedName {
            get { return _selectedName; }
            set {
                if (Equals(value, _selectedName)) return;
                _selectedName = value;
                OnPropertyChanged();
            }
        }

        private string _selectedMaterialName;

        public string SelectedMaterialName {
            get { return _selectedMaterialName; }
            set {
                if (Equals(value, _selectedMaterialName)) return;
                _selectedMaterialName = value;
                OnPropertyChanged();
            }
        }

        private string _selectedShaderName;

        public string SelectedShaderName {
            get { return _selectedShaderName; }
            set {
                if (Equals(value, _selectedShaderName)) return;
                _selectedShaderName = value;
                OnPropertyChanged();
            }
        }

        private TextureInformation[] _selectedTextures;

        public TextureInformation[] SelectedTextures {
            get { return _selectedTextures; }
            set {
                if (Equals(value, _selectedTextures)) return;
                _selectedTextures = value;
                OnPropertyChanged();
            }
        }

        public class TextureInformation {
            public string SlotName { get; set; }

            public string TextureName { get; set; }
        }

        private Kn5RenderableObject _selectedObject;

        public Kn5RenderableObject SelectedObject {
            get { return _selectedObject; }
            set {
                if (Equals(value, _selectedObject)) return;
                _selectedObject = value;
                OnPropertyChanged();

                if (value != null) {
                    if (_outlineBuffer == null) {
                        _outlineBuffer = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm, SampleDescription);
                        _outlineDepthBuffer = TargetResourceDepthTexture.Create();

                        if (InitiallyResized) {
                            _outlineBuffer.Resize(DeviceContextHolder, Width, Height);
                            _outlineDepthBuffer.Resize(DeviceContextHolder, Width, Height);
                        }
                    }

                    SelectedName = _selectedObject.OriginalNode.Name;
                    var material = Kn5.GetMaterial(_selectedObject.OriginalNode.MaterialId);
                    SelectedMaterialName = material?.Name;
                    SelectedShaderName = material?.ShaderName;
                    SelectedTextures = material?.TextureMappings.Select(x => new TextureInformation {
                        SlotName = x.Name,
                        TextureName = x.Texture
                    }).ToArray();
                } else {
                    SelectedName = null;
                    SelectedTextures = null;
                }
            }
        }

        private Kn5RenderableObject _previousSelectedFirstObject;
        private readonly List<Kn5RenderableObject> _previousSelectedObjects = new List<Kn5RenderableObject>();

        public void OnClick(Vector2 mousePosition) {
            var ray = Camera.GetPickingRay(mousePosition, new Vector2(Width, Height));

            var nodes = Scene.SelectManyRecursive(x => x as RenderableList)
                             .OfType<Kn5RenderableObject>()
                             .Where(node => {
                                 float d;
                                 return node.BoundingBox.HasValue && Ray.Intersects(ray, node.BoundingBox.Value, out d);
                             })
                             .Select(node => {
                                 var min = float.MaxValue;
                                 var found = false;

                                 var indices = node.Indices;
                                 var vertices = node.Vertices;
                                 var matrix = node.ParentMatrix;
                                 for (int i = 0, n = indices.Length / 3; i < n; i++) {
                                     var v0 = Vector3.TransformCoordinate(vertices[indices[i * 3]].Position, matrix);
                                     var v1 = Vector3.TransformCoordinate(vertices[indices[i * 3 + 1]].Position, matrix);
                                     var v2 = Vector3.TransformCoordinate(vertices[indices[i * 3 + 2]].Position, matrix);

                                     float distance;
                                     if (!Ray.Intersects(ray, v0, v1, v2, out distance) || distance >= min) continue;
                                     min = distance;
                                     found = true;
                                 }

                                 return found ? new {
                                     Node = node,
                                     Distance = min
                                 } : null;
                             })
                             .Where(x => x != null)
                             .OrderBy(x => x.Distance)
                             .Select(x => x.Node)
                             .ToList();

            if (nodes.Any()) {
                var first = nodes[0];
                if (first != _previousSelectedFirstObject) {
                    _previousSelectedObjects.Clear();
                    _previousSelectedObjects.Add(first);
                    _previousSelectedFirstObject = first;
                    SelectedObject = first;
                } else {
                    var filtered = nodes.Where(x => !_previousSelectedObjects.Contains(x)).ToList();
                    if (filtered.Any()) {
                        _previousSelectedObjects.Add(filtered[0]);
                        SelectedObject = filtered[0];
                    } else {
                        _previousSelectedObjects.Clear();
                        _previousSelectedObjects.Add(first);
                        SelectedObject = first;
                    }
                }
            } else {
                Deselect();
            }
        }

        public void Deselect() {
            SelectedObject = null;
            _previousSelectedObjects.Clear();
            _previousSelectedFirstObject = null;
        }

        public override void Dispose() {
            DisposeHelper.Dispose(ref _outlineBuffer);
            DisposeHelper.Dispose(ref _textBlock);
            _carHelper.Dispose();
            base.Dispose();
        }
    }
}
