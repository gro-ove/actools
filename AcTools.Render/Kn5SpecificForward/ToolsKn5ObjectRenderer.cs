using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
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

        bool OverrideTextureMaps(string textureName, double reflection, double gloss, double specular);

        Task SaveTexture(string filename, Color color);

        Task SaveTexture(string filename, Color color, double alpha);

        Task SaveTextureFlakes(string filename, Color color);

        Task SaveTextureMaps(string filename, string baseTextureName, double reflection, double gloss, double specular);
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

        private Kn5Material GetMaterial(IKn5RenderableObject obj) {
            if (ShowroomNode != null && ShowroomNode.GetAllChildren().Contains(obj)) {
                return ShowroomNode.OriginalFile.GetMaterial(obj.OriginalNode.MaterialId);
            }
            
            return CarNode?.GetMaterial(obj);
        }

        public Kn5 GetKn5(IKn5RenderableObject obj) {
            if (ShowroomNode != null && ShowroomNode.GetAllChildren().Contains(obj)) {
                return ShowroomNode.OriginalFile;
            }
            
            return CarNode?.GetKn5(obj);
        }

        private IKn5RenderableObject _selectedObject;

        [CanBeNull]
        public IKn5RenderableObject SelectedObject {
            get { return _selectedObject; }
            set {
                if (Equals(value, _selectedObject)) return;
                _selectedObject = value;
                OnPropertyChanged();

                if (value != null) {
                    PrepareOutlineBuffer();

                    SelectedName = _selectedObject.OriginalNode.Name;
                    SelectedMaterial = GetMaterial(value);
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

        private IKn5RenderableObject _previousSelectedFirstObject;
        private readonly List<IKn5RenderableObject> _previousSelectedObjects = new List<IKn5RenderableObject>();

        public void OnClick(Vector2 mousePosition) {
            var ray = Camera.GetPickingRay(mousePosition, new Vector2(ActualWidth, ActualHeight));

            var nodes = Scene.SelectManyRecursive(x => x as RenderableList)
                             .OfType<IKn5RenderableObject>()
                             .Where(x => x.IsInitialized)
                             .Select(node => {
                                 var f = node.CheckIntersection(ray);
                                 return f.HasValue ? new {
                                     Node = node,
                                     Distance = f.Value
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
            using (var image = new MagickImage(new MagickColor(color) { A = (byte)(255 * alpha) }, 4, 4)) {
                return OverrideTexture(textureName, image.ToByteArray(MagickFormat.Bmp));
            }
        }

        public Task SaveTexture(string filename, Color color, double alpha) {
            return SaveAndDispose(filename, new MagickImage(new MagickColor(color) { A = (byte)(255 * alpha) }, 16, 16));
        }

        public virtual bool OverrideTextureFlakes(string textureName, Color color) {
            return OverrideTexture(textureName, color);
        }

        public Task SaveTextureFlakes(string filename, Color color) {
            var image = new MagickImage(new MagickColor(color) { A = 250 }, 256, 256);
            image.AddNoise(NoiseType.Poisson, Channels.Alpha);
            return SaveAndDispose(filename, image);
        }

        private MagickImage _mapsBase;

        public bool OverrideTextureMaps(string textureName, double reflection, double gloss, double specular) {
            if (Kn5 == null) return false;

            if (_mapsBase == null) {
                Format format;
                var data = TextureReader.ToPng(DeviceContextHolder, Kn5.TexturesData[textureName], true, out format);

                _mapsBase = new MagickImage(data);
                if (_mapsBase.Width > 256 || _mapsBase.Height > 256) {
                    _mapsBase.Resize(256, 256);
                }

                _mapsBase.AutoLevel(Channels.Red);
                _mapsBase.AutoLevel(Channels.Green);
                _mapsBase.AutoLevel(Channels.Blue);
            }

            using (var image = _mapsBase.Clone()) {
                image.Evaluate(Channels.Red, EvaluateOperator.Multiply, specular);
                image.Evaluate(Channels.Green, EvaluateOperator.Multiply, gloss);
                image.Evaluate(Channels.Blue, EvaluateOperator.Multiply, reflection);
                return OverrideTexture(textureName, image.ToByteArray(MagickFormat.Png));
            }
        }

        public async Task SaveTextureMaps(string filename, string baseTextureName, double reflection, double gloss, double specular) {
            if (Kn5 == null) return;

            MagickImage image = null;
            await Task.Run(() => {
                Format format;
                var data = TextureReader.ToPng(DeviceContextHolder, Kn5.TexturesData[baseTextureName], true, out format);

                image = new MagickImage(data);
                image.AutoLevel(Channels.Red);
                image.AutoLevel(Channels.Green);
                image.AutoLevel(Channels.Blue);

                image.Evaluate(Channels.Red, EvaluateOperator.Multiply, specular);
                image.Evaluate(Channels.Green, EvaluateOperator.Multiply, gloss);
                image.Evaluate(Channels.Blue, EvaluateOperator.Multiply, reflection);
            });

            await SaveAndDispose(filename, image);
        }

        private Task SaveAndDispose(string filename, MagickImage image) {
            try {
                if (File.Exists(filename)) {
                    FileUtils.Recycle(filename);
                }

                image.Quality = 100;
                image.Settings.SetDefine(MagickFormat.Dds, "compression", "none");
                image.Settings.SetDefine(MagickFormat.Dds, "cluster-fit", "true");
                image.Settings.SetDefine(MagickFormat.Dds, "mipmaps", "false");
                var bytes = image.ToByteArray(MagickFormat.Dds);
                return FileUtils.WriteAllBytesAsync(filename, bytes);
            } finally {
                image.Dispose();
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void DisposeMagickNet() {
            DisposeHelper.Dispose(ref _mapsBase);
        }

        protected override void DisposeOverride() {
            DisposeHelper.Dispose(ref _outlineBuffer);
            DisposeHelper.Dispose(ref _outlineDepthBuffer);

            if (ImageUtils.IsMagickSupported) {
                try {
                    DisposeMagickNet();
                } catch {
                    // ignored
                }
            }

            base.DisposeOverride();
        }
    }
}