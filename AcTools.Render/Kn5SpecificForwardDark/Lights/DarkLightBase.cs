using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shadows;
using AcTools.Render.Base.Sprites;
using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DirectWrite;
using FontStyle = SlimDX.DirectWrite.FontStyle;
using FontWeight = SlimDX.DirectWrite.FontWeight;
using TextAlignment = AcTools.Render.Base.Sprites.TextAlignment;

namespace AcTools.Render.Kn5SpecificForwardDark.Lights {
    public enum DarkLightType {
        Point, Directional, Spot
    }

    public abstract class DarkLightBase : INotifyPropertyChanged, IMoveable, IDisposable {
        private string _displayName;

        public string DisplayName {
            get { return _displayName; }
            set {
                if (Equals(value, _displayName)) return;
                _displayName = value;
                OnPropertyChanged();
            }
        }

        private bool _enabled = true;

        public bool Enabled {
            get { return _enabled; }
            set {
                if (value == _enabled) return;
                _enabled = value;
                OnPropertyChanged();
            }
        }

        private bool _isVisibleInUi = true;

        public bool IsVisibleInUi {
            get { return _isVisibleInUi; }
            set {
                if (Equals(value, _isVisibleInUi)) return;
                _isVisibleInUi = value;
                OnPropertyChanged();
            }
        }

        public bool IsMovable { get; set; } = true;

        protected DarkLightBase(DarkLightType type) {
            _type = type;
        }

        #region Change type
        private DarkLightType _type;

        public DarkLightType Type {
            get { return _type; }
            set {
                if (Equals(value, _type)) return;
                _type = value;
                OnPropertyChanged();
            }
        }

        protected virtual DarkLightBase ChangeTypeOverride(DarkLightType newType) {
            switch (newType) {
                case DarkLightType.Point:
                    return new DarkPointLight();
                case DarkLightType.Directional:
                    return new DarkDirectionalLight();
                case DarkLightType.Spot:
                    return new DarkSpotLight();
                default:
                    throw new ArgumentOutOfRangeException(nameof(newType), newType, null);
            }
        }

        public DarkLightBase ChangeType(DarkLightType newType) {
            var result = ChangeTypeOverride(newType);
            result.Enabled = Enabled;
            result.Position = Position;
            result.DisplayName = DisplayName;
            result.UseShadows = UseShadows;
            result.UseHighQualityShadows = UseHighQualityShadows;
            result.Color = Color;
            result.Brightness = Brightness;
            result.IsMovable = IsMovable;
            result.IsVisibleInUi = IsVisibleInUi;
            result.ShadowsResolution = ShadowsResolution;
            return result;
        }
        #endregion

        #region Movement
        private MoveableHelper _movable;

        public MoveableHelper Movable => _movable ?? (_movable = CreateMoveableHelper());

        protected virtual MoveableHelper CreateMoveableHelper() {
            return new MoveableHelper(this, MoveableRotationAxis.All);
        }

        public void DrawMovementArrows(DeviceContextHolder holder, BaseCamera camera) {
            if (!IsMovable) return;
            Movable.ParentMatrix = Matrix.Translation(Position);
            Movable.Draw(holder, camera, SpecialRenderMode.Simple);
        }

        void IMoveable.Move(Vector3 delta) {
            if (!IsMovable) return;
            Position = Position + delta;
        }

        public abstract void Rotate(Quaternion delta);
        #endregion

        private Vector3 _position = new Vector3(0f, 2f, 0f);

        public Vector3 Position {
            get { return _position; }
            set {
                if (value.Equals(_position)) return;
                _position = value;
                InvalidateShadows();
                OnPropertyChanged();
            }
        }

        private Color _color = Color.White;

        public Color Color {
            get { return _color; }
            set {
                if (value.Equals(_color)) return;
                _color = value;
                OnPropertyChanged();
            }
        }

        private float _brightness = 1f;

        public float Brightness {
            get { return _brightness; }
            set {
                if (value.Equals(_brightness)) return;
                _brightness = value;
                OnPropertyChanged();
            }
        }

        protected void UpdateShadowsMode() {
            _shadowsMode = GetShadowsMode();
        }

        private bool _useShadows;

        public bool UseShadows {
            get { return _useShadows; }
            set {
                if (Equals(value, _useShadows)) return;
                _useShadows = value;
                UpdateShadowsMode();
                OnPropertyChanged();
            }
        }

        private bool _highQualityShadowsAvailable = true;

        public bool HighQualityShadowsAvailable {
            get { return _highQualityShadowsAvailable; }
            set {
                if (Equals(value, _highQualityShadowsAvailable)) return;
                _highQualityShadowsAvailable = value;
                UpdateShadowsMode();
                OnPropertyChanged();
            }
        }

        private bool _useHighQualityShadows;

        public bool UseHighQualityShadows {
            get { return _useHighQualityShadows && _highQualityShadowsAvailable; }
            set {
                if (Equals(value, _useHighQualityShadows)) return;
                _useHighQualityShadows = value;
                UpdateShadowsMode();
                OnPropertyChanged();
            }
        }

        private int _shadowsResolution = 1024;

        public int ShadowsResolution {
            get { return _shadowsResolution; }
            set {
                if (value == _shadowsResolution) return;
                _shadowsResolution = value;
                InvalidateShadows();
                OnPropertyChanged();
            }
        }

        protected abstract DarkShadowsMode GetShadowsMode();

        private DarkShadowsMode _shadowsMode;
        private int _shadowsOffset;

        protected abstract void UpdateShadowsOverride(DeviceContextHolder holder, Vector3 shadowsPosition, [CanBeNull] IShadowsDraw shadowsDraw);

        public void Update(DeviceContextHolder holder, Vector3 shadowsPosition, [CanBeNull] IShadowsDraw shadowsDraw) {
            // main shadows are rendered separately, by DarkKn5ObjectRenderer so all those
            // splits/PCSS/etc will be handled properly
            if (Enabled && _shadowsMode != DarkShadowsMode.Off && _shadowsMode != DarkShadowsMode.Main) {
                UpdateShadowsOverride(holder, shadowsPosition, shadowsDraw);
            }
        }

        protected abstract void SetShadowOverride(out float size, out Matrix matrix, out ShaderResourceView view, ref Vector4 nearFar);

        protected virtual void SetOverride(ref EffectDarkMaterial.Light light) {
            light.Color = Color.ToVector3() * Brightness;
            light.ShadowMode = _shadowsOffset == -1 ? 0 : (uint)((uint)_shadowsMode + _shadowsOffset);
        }

        private static readonly EffectDarkMaterial.Light[] LightsBuffer = new EffectDarkMaterial.Light[EffectDarkMaterial.MaxLighsAmount];
        private static readonly float[] ExtraShadowsSizesBuffer = new float[EffectDarkMaterial.MaxExtraShadows];
        private static readonly Vector4[] ExtraShadowsNearFarBuffer = new Vector4[EffectDarkMaterial.MaxExtraShadows];
        private static readonly Matrix[] ExtraShadowsMatricesBuffer = new Matrix[EffectDarkMaterial.MaxExtraShadows];
        private static readonly ShaderResourceView[] ExtraShadowsViewsBuffer = new ShaderResourceView[EffectDarkMaterial.MaxExtraShadows];

        public static void FlipPreviousY(EffectDarkMaterial effect) {
            for (var i = 0; i < LightsBuffer.Length; i++) {
                LightsBuffer[i].DirectionW.Y *= -1f;
                LightsBuffer[i].PosW.Y *= -1f;
            }

            effect.FxLights.SetArray(LightsBuffer);
        }

        public static void ToShader(EffectDarkMaterial effect, DarkLightBase[] lights, int count) {
            var shadowOffset = 0;

            for (var i = count - 1; i >= 0; i--) {
                var l = lights[i];
                if (l.Enabled && l._shadowsMode != DarkShadowsMode.Off && l._shadowsMode != DarkShadowsMode.Main) {
                    lights[i]._shadowsOffset = -1;
                }
            }

            for (var i = 0; i < count; i++) {
                var l = lights[i];
                if (l.Enabled && l._shadowsMode == DarkShadowsMode.ExtraSmooth) {
                    var offset = shadowOffset++;
                    l._shadowsOffset = offset;

                    if (offset >= EffectDarkMaterial.MaxExtraShadows) {
                        l.UseShadows = false;
                    } else {
                        l.SetShadowOverride(out ExtraShadowsSizesBuffer[offset], out ExtraShadowsMatricesBuffer[offset],
                                out ExtraShadowsViewsBuffer[offset], ref ExtraShadowsNearFarBuffer[offset]);
                        if (offset >= EffectDarkMaterial.MaxExtraShadowsSmooth) {
                            l.UseHighQualityShadows = false;
                        }
                    }
                }
            }

            for (var i = 0; i < count; i++) {
                var l = lights[i];
                if (l.Enabled && (l._shadowsMode == DarkShadowsMode.ExtraFast || l._shadowsMode == DarkShadowsMode.ExtraPoint) && l._shadowsOffset == -1) {
                    var offset = shadowOffset++;
                    l._shadowsOffset = offset;

                    if (offset >= EffectDarkMaterial.MaxExtraShadows) {
                        l.UseShadows = false;
                    } else {
                        l.SetShadowOverride(out ExtraShadowsSizesBuffer[offset], out ExtraShadowsMatricesBuffer[offset],
                                out ExtraShadowsViewsBuffer[offset], ref ExtraShadowsNearFarBuffer[offset]);
                    }
                }
            }

            var j = 0;
            for (var i = 0; i < count && j < EffectDarkMaterial.MaxLighsAmount; i++) {
                var l = lights[i];
                if (l.Enabled) {
                    l.SetOverride(ref LightsBuffer[j++]);
                }
            }

            for (; j < EffectDarkMaterial.MaxLighsAmount; j++) {
                LightsBuffer[j].Type = EffectDarkMaterial.LightOff;
            }

            effect.FxLights.SetArray(LightsBuffer);

            if (effect.FxExtraShadowMapSize.IsValid) {
                effect.FxExtraShadowMapSize.Set(ExtraShadowsSizesBuffer);
                effect.FxExtraShadowViewProj.SetMatrixArray(ExtraShadowsMatricesBuffer);
                effect.FxExtraShadowMaps.SetResourceArray(ExtraShadowsViewsBuffer);
                effect.FxExtraShadowCubeMaps.SetResourceArray(ExtraShadowsViewsBuffer);
                effect.FxExtraShadowNearFar.Set(ExtraShadowsNearFarBuffer);
            }
        }

        public abstract void InvalidateShadows();

        public abstract void DrawDummy(IDeviceContextHolder holder, ICamera camera);

        public void Dispose() {
            DisposeOverride();
            DisposeHelper.Dispose(ref _debugText);
        }

        protected abstract void DisposeOverride();

        #region Removal
        private bool _isDeleted;

        public bool IsDeleted {
            get { return _isDeleted; }
            set {
                if (value == _isDeleted) return;
                _isDeleted = value;
                OnPropertyChanged();
            }
        }

        private ICommand _deleteCommand;

        public ICommand DeleteCommand => _deleteCommand ?? (_deleteCommand = new DelegateCommand(() => {
            IsDeleted = true;
        }));
        #endregion

        #region INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region Label
        private TextBlockRenderer _debugText;

        private void DrawText(string text, Matrix objectTransform, ICamera camera, Vector2 screenSize, Color4 color) {
            var onScreenPosition = Vector3.TransformCoordinate(Vector3.Zero, objectTransform * camera.ViewProj) * 0.5f +
                    new Vector3(0.5f);
            onScreenPosition.Y = 1f - onScreenPosition.Y;
            _debugText.DrawString(text,
                    new RectangleF(onScreenPosition.X * screenSize.X - 100f, onScreenPosition.Y * screenSize.Y - 70f, 200f, 200f),
                    TextAlignment.HorizontalCenter | TextAlignment.VerticalCenter, 12f, color,
                    CoordinateType.Absolute);
        }

        public void DrawSprites(SpriteRenderer sprite, ICamera camera, Vector2 screenSize) {
            if (_debugText == null) {
                _debugText = new TextBlockRenderer(sprite, "Arial", FontWeight.Normal, FontStyle.Normal, FontStretch.Normal, 16f);
            }

            DrawText(DisplayName, Matrix.Translation(Position), camera, screenSize,
                    Enabled ? new Color4(1f, 1f, 1f, 0f) : new Color4(1f, 0.4f, 0.4f, 0f));
        }
        #endregion
    }
}