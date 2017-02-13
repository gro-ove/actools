using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcTools.Kn5File;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Shaders;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using ImageMagick;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Kn5SpecificForward {
    public interface IPaintShopRenderer {
        bool OverrideTexture(string textureName, byte[] textureBytes);

        bool OverrideTexture(string textureName, Color color);

        bool OverrideTexture(string textureName, Color color, double alpha);

        bool OverrideTextureFlakes(string textureName, Color color);

        bool OverrideTextureMaps(string textureName, double reflection, double blur, double specular);

        Task SaveTexture(string filename, Color color);

        Task SaveTexture(string filename, Color color, double alpha);

        Task SaveTextureFlakes(string filename, Color color);
    }

    public class ToolsKn5ObjectRenderer : ForwardKn5ObjectRenderer, IPaintShopRenderer {
        public ToolsKn5ObjectRenderer(CarDescription car, string showroomKn5Filename = null) : base(car, showroomKn5Filename) {
            UseSprite = false;
        }

        protected override void ClearBeforeChangingCar() {
            SelectedObject = null;
            base.ClearBeforeChangingCar();
        }

        public bool LiveReload {
            get { return CarNode?.LiveReload ?? false; }
            set {
                if (CarNode == null || Equals(value, CarNode.LiveReload)) return;
                CarNode.LiveReload = value;
                OnPropertyChanged();
            }
        }

        public bool MagickOverride {
            get { return CarNode?.MagickOverride ?? false; }
            set {
                if (CarNode == null || Equals(value, CarNode.MagickOverride)) return;
                CarNode.MagickOverride = value;
                OnPropertyChanged();
            }
        }

        private bool _ambientShadowHighlight;

        public bool AmbientShadowHighlight {
            get { return _ambientShadowHighlight; }
            set {
                if (Equals(value, _ambientShadowHighlight)) return;
                _ambientShadowHighlight = value;
                OnPropertyChanged();

                if (value) {
                    PrepareOutlineBuffer();
                }
            }
        }

        private bool _ambientShadowSizeChanged;

        public bool AmbientShadowSizeChanged {
            get { return _ambientShadowSizeChanged; }
            set {
                if (Equals(value, _ambientShadowSizeChanged)) return;
                _ambientShadowSizeChanged = value;
                OnPropertyChanged();
            }
        }

        public float AmbientShadowWidth {
            get { return CarNode?.AmbientShadowSize.X ?? 0f; }
            set {
                if (CarNode == null) return;
                if (Equals(value, CarNode.AmbientShadowSize.X)) return;
                CarNode.AmbientShadowSize = new Vector3(value, CarNode.AmbientShadowSize.Y, CarNode.AmbientShadowSize.Z);
                OnPropertyChanged();
                AmbientShadowSizeChanged = true;
            }
        }

        public float AmbientShadowLength {
            get { return CarNode?.AmbientShadowSize.Z ?? 0f; }
            set {
                if (CarNode == null) return;
                if (Equals(value, CarNode.AmbientShadowSize.Z)) return;
                CarNode.AmbientShadowSize = new Vector3(CarNode.AmbientShadowSize.X, CarNode.AmbientShadowSize.Y, value);
                OnPropertyChanged();
                AmbientShadowSizeChanged = true;
            }
        }

        public void ResetAmbientShadowSize() {
            if (CarNode == null) return;
            CarNode.ResetAmbientShadowSize();
            OnPropertyChanged(nameof(AmbientShadowWidth));
            OnPropertyChanged(nameof(AmbientShadowLength));
            AmbientShadowSizeChanged = false;
        }

        public void FitAmbientShadowSize() {
            if (CarNode == null) return;
            CarNode.FitAmbientShadowSize();
            OnPropertyChanged(nameof(AmbientShadowWidth));
            OnPropertyChanged(nameof(AmbientShadowLength));
            AmbientShadowSizeChanged = true;
        }

        private TargetResourceTexture _outlineBuffer;
        private TargetResourceDepthTexture _outlineDepthBuffer;

        protected override void ResizeInner() {
            base.ResizeInner();
            _outlineBuffer?.Resize(DeviceContextHolder, Width, Height, null);
            _outlineDepthBuffer?.Resize(DeviceContextHolder, Width, Height, null);
        }

        protected override void DrawPrepare() {
            base.DrawPrepare();

            var highlighted = AmbientShadowHighlight ? (IRenderableObject)CarNode?.AmbientShadowNode : SelectedObject;
            if (highlighted == null || _outlineBuffer == null) return;

            DeviceContext.ClearDepthStencilView(_outlineDepthBuffer.DepthView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1f, 0);
            DeviceContext.OutputMerger.SetTargets(_outlineDepthBuffer.DepthView);
            DeviceContext.Rasterizer.State = DeviceContextHolder.States.DoubleSidedState;
            
            highlighted.Draw(DeviceContextHolder, ActualCamera, SpecialRenderMode.Outline);

            DeviceContext.ClearRenderTargetView(_outlineBuffer.TargetView, Color.Transparent);
            DeviceContext.OutputMerger.SetTargets(_outlineBuffer.TargetView);

            var effect = DeviceContextHolder.GetEffect<EffectPpOutline>();
            effect.FxDepthMap.SetResource(_outlineDepthBuffer.View);
            effect.FxScreenSize.Set(new Vector4(ActualWidth, ActualHeight, 1f / ActualWidth, 1f / ActualHeight));
            DeviceContextHolder.PrepareQuad(effect.LayoutPT);
            effect.TechOutline.DrawAllPasses(DeviceContext, 6);
        }

        protected override void DrawAfter() {
            base.DrawAfter();
            if (!AmbientShadowHighlight && SelectedObject == null || _outlineBuffer == null) return;

            var effect = DeviceContextHolder.GetEffect<EffectPpBasic>();
            DeviceContext.OutputMerger.BlendState = DeviceContextHolder.States.TransparentBlendState;
            DeviceContextHolder.PrepareQuad(effect.LayoutPT);
            effect.FxInputMap.SetResource(_outlineBuffer.View);

            DeviceContext.Rasterizer.State = null;
            DeviceContext.OutputMerger.DepthStencilState = DeviceContextHolder.States.DisabledDepthState;
            effect.TechCopy.DrawAllPasses(DeviceContext, 6);
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

        private void PrepareOutlineBuffer() {
            if (_outlineBuffer != null) return;
            _outlineBuffer = TargetResourceTexture.Create(Format.R8G8B8A8_UNorm);
            _outlineDepthBuffer = TargetResourceDepthTexture.Create();

            if (!InitiallyResized) return;
            _outlineBuffer.Resize(DeviceContextHolder, Width, Height, null);
            _outlineDepthBuffer.Resize(DeviceContextHolder, Width, Height, null);
        }

        private Kn5RenderableObject _selectedObject;

        [CanBeNull]
        public Kn5RenderableObject SelectedObject {
            get { return _selectedObject; }
            set {
                if (Equals(value, _selectedObject)) return;
                _selectedObject = value;
                OnPropertyChanged();

                if (value != null) {
                    PrepareOutlineBuffer();

                    SelectedName = _selectedObject.OriginalNode.Name;
                    SelectedMaterial = Kn5.GetMaterial(_selectedObject.OriginalNode.MaterialId);
                    SelectedTextures = SelectedMaterial?.TextureMappings.Select(x => new TextureInformation {
                        SlotName = x.Name,
                        TextureName = x.Texture
                    }).ToArray();
                } else {
                    SelectedName = null;
                    SelectedMaterial = null;
                    SelectedTextures = null;
                }
            }
        }

        private Kn5Material _selectedMaterial;

        public Kn5Material SelectedMaterial {
            get { return _selectedMaterial; }
            set {
                if (Equals(value, _selectedMaterial)) return;
                _selectedMaterial = value;
                OnPropertyChanged();
            }
        }

        private Kn5RenderableObject _previousSelectedFirstObject;
        private readonly List<Kn5RenderableObject> _previousSelectedObjects = new List<Kn5RenderableObject>();

        public void OnClick(Vector2 mousePosition) {
            var ray = Camera.GetPickingRay(mousePosition, new Vector2(ActualWidth, ActualHeight));

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

        public bool OverrideTexture(string textureName, byte[] textureBytes) {
            if (CarNode == null) return true;
            return CarNode.OverrideTexture(DeviceContextHolder, textureName, textureBytes);
        }

        public bool OverrideTexture(string textureName, Color color) {
            using (var image = new MagickImage(new MagickColor(color), 4, 4)) {
                return OverrideTexture(textureName, image.ToByteArray(MagickFormat.Bmp));
            }
        }

        public Task SaveTexture(string filename, Color color) {
            return SaveAndDispose(filename, new MagickImage(new MagickColor(color), 16, 16));
        }

        public bool OverrideTexture(string textureName, Color color, double alpha) {
            using (var image = new MagickImage(new MagickColor(color) { A = (ushort)(ushort.MaxValue * alpha) }, 4, 4)) {
                return OverrideTexture(textureName, image.ToByteArray(MagickFormat.Bmp));
            }
        }

        public Task SaveTexture(string filename, Color color, double alpha) {
            return SaveAndDispose(filename, new MagickImage(new MagickColor(color) { A = (ushort)(ushort.MaxValue * alpha) }, 16, 16));
        }

        public virtual bool OverrideTextureFlakes(string textureName, Color color) {
            return OverrideTexture(textureName, color);
        }

        public bool OverrideTextureMaps(string textureName, double reflection, double blur, double specular) {
            using (var image = new MagickImage(Kn5.TexturesData[textureName])) {
                if (image.Width > 512 || image.Height > 512) {
                    image.Resize(512, 512);
                }

                image.BrightnessContrast(reflection, 1d, Channels.Red);
                image.BrightnessContrast(blur, 1d, Channels.Green);
                image.BrightnessContrast(specular, 1d, Channels.Blue);

                return OverrideTexture(textureName, image.ToByteArray(MagickFormat.Bmp));
            }
        }

        public Task SaveTextureFlakes(string filename, Color color) {
            var image = new MagickImage(new MagickColor(color) { A = 250 }, 256, 256);
            image.AddNoise(NoiseType.Poisson, Channels.Alpha);
            return SaveAndDispose(filename, image);
        }

        private Task SaveAndDispose(string filename, MagickImage image) {
            try {
                if (File.Exists(filename)) {
                    FileUtils.Recycle(filename);
                }

                image.SetDefine(MagickFormat.Dds, "compression", "none");
                image.Quality = 100;
                var bytes = image.ToByteArray(MagickFormat.Dds);
                return FileUtils.WriteAllBytesAsync(filename, bytes);
            } finally {
                image.Dispose();
            }
        }

        public override void Dispose() {
            DisposeHelper.Dispose(ref _outlineBuffer);
            DisposeHelper.Dispose(ref _outlineDepthBuffer);
            base.Dispose();
        }
    }
}