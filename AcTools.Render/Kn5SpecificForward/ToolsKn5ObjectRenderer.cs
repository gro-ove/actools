using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using AcTools.Kn5File;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.TargetTextures;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Shaders;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DXGI;

namespace AcTools.Render.Kn5SpecificForward {
    public partial class ToolsKn5ObjectRenderer : ForwardKn5ObjectRenderer {
        public ToolsKn5ObjectRenderer(CarDescription car, string showroomKn5Filename = null) : base(car, showroomKn5Filename) { }

        protected override void ClearBeforeChangingCar() {
            SelectedObject = null;
            base.ClearBeforeChangingCar();
        }

        public bool LiveReload {
            get => CarNode?.LiveReload ?? false;
            set {
                if (CarNode == null || Equals(value, CarNode.LiveReload)) return;
                CarNode.LiveReload = value;
                OnPropertyChanged();
            }
        }

        private bool? _magickOverideLater;

        public bool MagickOverride {
            get => _magickOverideLater ?? CarNode?.MagickOverride ?? false;
            set {
                if (CarNode == null) {
                    OnPropertyChanged();
                    _magickOverideLater = value;
                    return;
                }
                if (Equals(value, CarNode.MagickOverride)) return;
                CarNode.MagickOverride = value;
                OnPropertyChanged();
            }
        }

        protected override void CopyValues(Kn5RenderableCar newCar, Kn5RenderableCar oldCar) {
            base.CopyValues(newCar, oldCar);
            if (_magickOverideLater.HasValue) {
                newCar.MagickOverride = _magickOverideLater.Value;
                _magickOverideLater = null;
            }
        }

        private bool _ambientShadowHighlight;

        public bool AmbientShadowHighlight {
            get => _ambientShadowHighlight;
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
            get => _ambientShadowSizeChanged;
            set {
                if (Equals(value, _ambientShadowSizeChanged)) return;
                _ambientShadowSizeChanged = value;
                OnPropertyChanged();
            }
        }

        public float AmbientShadowWidth {
            get => CarNode?.AmbientShadowSize.X ?? 0f;
            set {
                if (CarNode == null) return;
                if (Equals(value, CarNode.AmbientShadowSize.X)) return;
                CarNode.AmbientShadowSize = new Vector3(value, CarNode.AmbientShadowSize.Y, CarNode.AmbientShadowSize.Z);
                OnPropertyChanged();
                AmbientShadowSizeChanged = true;
            }
        }

        public float AmbientShadowLength {
            get => CarNode?.AmbientShadowSize.Z ?? 0f;
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
            TestCameraMoved();
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

        protected override void DrawSpritesInner() {
            CarNode?.DrawSprites(Sprite, Camera, new Vector2(ActualWidth, ActualHeight));
            base.DrawSpritesInner();
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
            get => _selectedName;
            set {
                if (Equals(value, _selectedName)) return;
                _selectedName = value;
                OnPropertyChanged();
            }
        }

        private TextureInformation[] _selectedTextures;

        public TextureInformation[] SelectedTextures {
            get => _selectedTextures;
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

            return (from carSlot in CarSlots
                    where carSlot.CarNode?.ContainsNode(obj) == true
                    select carSlot.CarNode?.GetMaterial(obj)).FirstOrDefault();
        }

        public Kn5 GetKn5(IKn5RenderableObject obj) {
            if (ShowroomNode != null && ShowroomNode.GetAllChildren().Contains(obj)) {
                return ShowroomNode.OriginalFile;
            }

            return (from carSlot in CarSlots
                    where carSlot.CarNode?.ContainsNode(obj) == true
                    select carSlot.CarNode?.GetKn5(obj)).FirstOrDefault();
        }

        private IKn5RenderableObject _selectedObject;

        [CanBeNull]
        public IKn5RenderableObject SelectedObject {
            get => _selectedObject;
            set {
                if (Equals(value, _selectedObject)) return;
                _selectedObject = value;
                OnPropertyChanged();
                IsDirty = true;

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

        private readonly List<IRenderableObject> _hidden = new List<IRenderableObject>(10);

        public void ToggleSelected() {
            var selected = SelectedObject;
            if (selected == null) return;

            selected.IsEnabled = !selected.IsEnabled;
            if (selected.IsEnabled) {
                _hidden.Remove(selected);
            } else {
                _hidden.Add(selected);
            }

            SetShadowsDirty();
            SetReflectionCubemapDirty();
        }

        public void UnhideAll() {
            if (_hidden.Count == 0) return;
            foreach (var o in _hidden) {
                o.IsEnabled = true;
            }
            _hidden.Clear();

            SetShadowsDirty();
            SetReflectionCubemapDirty();
        }

        public IEnumerable<string> GetHiddenNodesNames() {
            return _hidden.Select(x => x.Name);
        }

        private Kn5Material _selectedMaterial;

        public Kn5Material SelectedMaterial {
            get => _selectedMaterial;
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
                    OnClickSelect(first);
                } else {
                    var filtered = nodes.Where(x => !_previousSelectedObjects.Contains(x)).ToList();
                    if (filtered.Any()) {
                        _previousSelectedObjects.Add(filtered[0]);
                        OnClickSelect(filtered[0]);
                    } else {
                        _previousSelectedObjects.Clear();
                        _previousSelectedObjects.Add(first);
                        OnClickSelect(first);
                    }
                }
            } else {
                Deselect();
            }
        }

        protected virtual void OnClickSelect(IKn5RenderableObject selected) {
            SelectedObject = selected;
        }

        public void Deselect() {
            SelectedObject = null;
            _previousSelectedObjects.Clear();
            _previousSelectedFirstObject = null;
        }

        protected override void DisposeOverride() {
            DisposeHelper.Dispose(ref _outlineBuffer);
            DisposeHelper.Dispose(ref _outlineDepthBuffer);
            DisposePaintShop();
            base.DisposeOverride();
        }
    }
}