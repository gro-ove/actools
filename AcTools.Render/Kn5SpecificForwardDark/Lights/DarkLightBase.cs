using System;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Shadows;
using AcTools.Render.Base.Sprites;
using AcTools.Render.Base.Utils;
using AcTools.Render.Shaders;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SlimDX;
using SlimDX.Direct3D11;
using SlimDX.DirectWrite;
using FontStyle = SlimDX.DirectWrite.FontStyle;
using FontWeight = SlimDX.DirectWrite.FontWeight;
using TextAlignment = AcTools.Render.Base.Sprites.TextAlignment;

namespace AcTools.Render.Kn5SpecificForwardDark.Lights {
    public enum DarkLightType {
        Point = 0, Directional = 1, Spot = 2
    }

    public class DarkLightTag {
        private const int CarId = 1;
        private const int ShowroomId = 2000000;
        private const int ExtraId = 3000000;

        private readonly int _id;

        public static readonly DarkLightTag Main = new DarkLightTag(0);
        public static readonly DarkLightTag Car = new DarkLightTag(CarId);
        public static readonly DarkLightTag Showroom = new DarkLightTag(ShowroomId);
        public static readonly DarkLightTag Extra = new DarkLightTag(ExtraId);

        public static DarkLightTag GetCarTag(int carId) {
            return new DarkLightTag(CarId + carId.Abs());
        }

        private DarkLightTag(int id) {
            _id = id;
            IsCarTag = id >= CarId && id < ShowroomId;
            IsShowroomTag = id >= ShowroomId && id < ExtraId;
        }

        public override string ToString() {
            return IsCarTag ? "Car" : IsShowroomTag ? "Showroom" : _id == 0 ? "Main" : "Extra";
        }

        public readonly bool IsCarTag;
        public readonly bool IsShowroomTag;

        protected bool Equals(DarkLightTag other) {
            return _id == other._id;
        }

        public override bool Equals(object obj) {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((DarkLightTag)obj);
        }

        public override int GetHashCode() {
            return _id;
        }

        public int CompareTo(DarkLightTag other) {
            return _id.CompareTo(other?._id ?? -1);
        }

        public static bool operator <(DarkLightTag e1, DarkLightTag e2) {
            return e1?._id < e2?._id;
        }

        public static bool operator >(DarkLightTag e1, DarkLightTag e2) {
            return e1?._id > e2?._id;
        }

        public static bool operator <=(DarkLightTag e1, DarkLightTag e2) {
            return e1?._id <= e2?._id;
        }

        public static bool operator >=(DarkLightTag e1, DarkLightTag e2) {
            return e1?._id >= e2?._id;
        }

        public static bool operator ==(DarkLightTag e1, DarkLightTag e2) {
            return e1?._id == e2?._id;
        }

        public static bool operator !=(DarkLightTag e1, DarkLightTag e2) {
            return e1?._id != e2?._id;
        }
    }

    public abstract class DarkLightBase : INotifyPropertyChanged, IMoveable, IDisposable {
        public static int OptionDefaultShadowsResolution = 1024;

        private string _displayName;

        public string DisplayName {
            get { return _displayName; }
            set {
                if (Equals(value, _displayName)) return;
                _displayName = value;
                OnPropertyChanged();
                UpdateActs();
            }
        }

        private bool _enabled = true;
        private bool _smoothChanging;

        public bool Enabled {
            get { return _enabled; }
            set {
                if (value == _enabled) return;
                _enabled = value;
                OnPropertyChanged();

                if (SmoothDelay?.TotalSeconds > 0d) {
                    _smoothChanging = true;
                }
            }
        }

        public bool ActuallyEnabled => _enabled || _smoothChanging;

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

        private static DarkLightBase CreateByType(DarkLightType newType) {
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

        protected virtual DarkLightBase ChangeTypeOverride(DarkLightType newType) {
            return CreateByType(newType);
        }

        public DarkLightBase Clone() {
            const string copyPostfix = " (Copy)";
            const string copyNPostfix = " (Copy #{0})";

            var result = ChangeType(Type);
            var name = DisplayName;
            if (name.EndsWith(copyPostfix)) {
                name = name.ApartFromLast(copyPostfix) + string.Format(copyNPostfix, 2);
            } else {
                var m = Regex.Match(name, @" \(Copy #(\d+)\)");
                if (m.Success) {
                    var n = (FlexibleParser.TryParseInt(m.Groups[1].Value) ?? 2) + 1;
                    name = name.Substring(0, name.Length - m.Length) + string.Format(copyNPostfix, n);
                } else {
                    name = name + copyPostfix;
                }
            }

            DisplayName = name;
            return result;
        }

        IMoveable IMoveable.Clone() {
            return Clone();
        }

        public DarkLightBase ChangeType(DarkLightType newType) {
            var result = ChangeTypeOverride(newType);
            result.Tag = Tag;
            result.ParentMatrix = ParentMatrix;
            result._enabled = Enabled; // to avoid smooth enabling
            result.Position = Position;
            result.DisplayName = DisplayName;
            result.UseShadows = UseShadows;
            result.UseHighQualityShadows = UseHighQualityShadows;
            result.Color = Color;
            result.Brightness = Brightness;
            result.IsMovable = IsMovable;
            result.IsVisibleInUi = IsVisibleInUi;
            result.ShadowsResolution = ShadowsResolution;
            result.BrightnessMultiplier = BrightnessMultiplier;
            result.AttachedTo = AttachedTo;
            result.SmoothDelay = SmoothDelay;
            return result;
        }
        #endregion

        #region Save/load
        protected static string ColorToHexString(Color color, bool alphaChannel = false) {
            return $"#{(alphaChannel ? color.A.ToString("X2") : string.Empty)}{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        [CanBeNull]
        protected static Color? HexStringToColor(string s) {
            if (s == null) return null;
            try {
                return ColorTranslator.FromHtml(s);
            } catch (Exception) {
                return null;
            }
        }

        protected static string VectorToString(Vector3 vector) {
            return $"{vector.X.ToInvariantString()},{vector.Y.ToInvariantString()},{vector.Z.ToInvariantString()}";
        }

        [CanBeNull]
        protected static Vector3? StringToVector(string s) {
            if (s == null) return null;
            try {
                return s.Split(',').Select(x => (float)(FlexibleParser.TryParseDouble(x) ?? 0f)).ToArray().ToVector3();
            } catch (Exception) {
                return null;
            }
        }

        private string _attachedTo;

        [CanBeNull]
        public string AttachedTo {
            get { return _attachedTo; }
            set {
                value = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
                if (Equals(value, _attachedTo)) return;
                _attachedTo = value;
                AttachedToObject = null;
                OnPropertyChanged();
            }
        }

        private bool _attachedToSelect;

        public bool AttachedToSelect {
            get { return _attachedToSelect; }
            set {
                if (Equals(value, _attachedToSelect)) return;
                _attachedToSelect = value;
                OnPropertyChanged();
            }
        }

        private TimeSpan? _smoothDelay;

        public TimeSpan? SmoothDelay {
            get { return _smoothDelay; }
            set {
                if (Equals(value, _smoothDelay)) return;
                _smoothDelay = value;
                OnPropertyChanged();
            }
        }

        private float _asHeadlightMultiplier;

        public float AsHeadlightMultiplier {
            get { return _asHeadlightMultiplier; }
            set {
                if (Equals(value, _asHeadlightMultiplier)) return;
                _asHeadlightMultiplier = value;
                OnPropertyChanged();
            }
        }

        internal IRenderableObject AttachedToObject { get; set; }

        internal Matrix AttachedToRelativeMatrix { get; set; }

        protected virtual void SerializeOverride(JObject obj) {
            obj["pos"] = VectorToString(Position);
            obj["color"] = ColorToHexString(Color);
            obj["brightness"] = Brightness;

            if (!Enabled) {
                obj["enabled"] = Enabled;
            }

            obj["name"] = DisplayName;

            if (UseShadows) {
                obj["shadows"] = UseShadows;
            }

            if (UseHighQualityShadows) {
                obj["shadowsSmooth"] = UseHighQualityShadows;
            }

            if (AsHeadlightMultiplier != 0f) {
                obj["headlightsMultiplier"] = AsHeadlightMultiplier;
            }

            if (!string.IsNullOrWhiteSpace(AttachedTo)) {
                obj["attached"] = AttachedTo;
            }

            if (SmoothDelay.HasValue) {
                obj["smoothDelay"] = SmoothDelay.Value.TotalSeconds;
            }

            if (ShadowsResolution != OptionDefaultShadowsResolution) {
                obj["shadowsResolution"] = ShadowsResolution;
            }

            if (!IsMovable) {
                obj["movable"] = IsMovable;
            }

            if (!IsVisibleInUi) {
                obj["visible"] = IsVisibleInUi;
            }
        }

        protected virtual void DeserializeOverride(JObject obj) {
            Position = StringToVector((string)obj["pos"]) ?? Vector3.UnitY;
            Color = HexStringToColor((string)obj["color"]) ?? Color.White;
            Brightness = obj["brightness"] != null ? (float)obj["brightness"] : 1f;
            AsHeadlightMultiplier = obj["headlightsMultiplier"] != null ? (float)obj["headlightsMultiplier"] : 0f;
            _enabled = obj["enabled"] == null || (bool)obj["enabled"]; // to avoid smooth enabling
            DisplayName = (string)obj["name"] ?? DisplayName;
            UseShadows = obj["shadows"] != null && (bool)obj["shadows"];
            UseHighQualityShadows = obj["shadowsSmooth"] != null && (bool)obj["shadowsSmooth"];
            ShadowsResolution = obj["shadowsResolution"] != null ? (int)obj["shadowsResolution"] : OptionDefaultShadowsResolution;
            SmoothDelay = obj["smoothDelay"] != null ? TimeSpan.FromSeconds((double)obj["smoothDelay"]) : (TimeSpan?)null;
            AttachedTo = (string)obj["attached"];
            IsMovable = obj["movable"] == null || (bool)obj["movable"];
            IsVisibleInUi = obj["visible"] == null || (bool)obj["visible"];
        }

        [CanBeNull]
        public JObject SerializeToJObject() {
            try {
                var result = new JObject();
                SerializeOverride(result);
                result["type"] = (int)Type;
                return result;
            } catch (Exception e) {
                AcToolsLogging.Write(e);
                return null;
            }
        }

        [CanBeNull]
        public static DarkLightBase Deserialize([NotNull] JObject obj) {
            try {
                var result = CreateByType((DarkLightType)(int)obj["type"]);
                result.DeserializeOverride(obj);
                return result;
            } catch (Exception e) {
                AcToolsLogging.Write(e);
                return null;
            }
        }

        [CanBeNull]
        public string Serialize() {
            try {
                return SerializeToJObject()?.ToString(Formatting.None);
            } catch (Exception e) {
                AcToolsLogging.Write(e);
                return null;
            }
        }

        [CanBeNull]
        public static DarkLightBase Deserialize([NotNull] string data) {
            try {
                return Deserialize(JObject.Parse(data));
            } catch (Exception e) {
                AcToolsLogging.Write(e);
                return null;
            }
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
            Movable.ParentMatrix = Matrix.Translation(ActualPosition);
            Movable.Draw(holder, camera, SpecialRenderMode.Simple);
        }

        void IMoveable.Move(Vector3 delta) {
            if (!IsMovable) return;
            var parentMatrixInvert = Matrix.Invert(ParentMatrix);
            Position = Vector3.TransformCoordinate(ActualPosition + delta, parentMatrixInvert);
        }

        public abstract void Rotate(Quaternion delta);
        #endregion

        private Matrix _parentMatrix = Matrix.Identity;

        public Matrix ParentMatrix {
            get { return _parentMatrix; }
            set {
                if (Equals(value, _parentMatrix)) return;
                _parentMatrix = value;
                OnParentMatrixChanged();
                OnPropertyChanged();
            }
        }

        protected virtual void OnParentMatrixChanged() {
            _actualPosition = null;
            InvalidateShadows();
        }

        private Vector3 _position = new Vector3(0f, 2f, 0f);

        public Vector3 Position {
            get { return _position; }
            set {
                if (value.Equals(_position)) return;
                _position = value;
                _actualPosition = null;
                InvalidateShadows();
                OnPropertyChanged();
            }
        }

        private Vector3? _actualPosition;
        protected Vector3 ActualPosition => _actualPosition ?? (_actualPosition = Vector3.TransformCoordinate(Position, ParentMatrix)).Value;

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

        public bool ShadowsActive => UseShadows && _shadowsOffset != -1;

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

        private int _shadowsResolution = OptionDefaultShadowsResolution;

        public int ShadowsResolution {
            get { return _shadowsResolution; }
            set {
                if (value == _shadowsResolution) return;
                _shadowsResolution = value;
                InvalidateShadows();
                OnPropertyChanged();
            }
        }

        protected DarkShadowsMode GetShadowsMode() {
            return UseShadows ? HighQualityShadowsAvailable && UseHighQualityShadows ? DarkShadowsMode.ExtraSmooth :
                    DarkShadowsMode.ExtraFast : DarkShadowsMode.Off;
        }

        private DarkShadowsMode _shadowsMode;
        private int _shadowsOffset;

        protected abstract void UpdateShadowsOverride(DeviceContextHolder holder, Vector3 shadowsPosition, [CanBeNull] IShadowsDraw shadowsDraw);

        public void Update(DeviceContextHolder holder, Vector3 shadowsPosition, [CanBeNull] IShadowsDraw shadowsDraw) {
            // main shadows are rendered separately, by DarkKn5ObjectRenderer so all those
            // splits/PCSS/etc will be handled properly
            if (ActuallyEnabled && _shadowsMode != DarkShadowsMode.Off && _shadowsMode != DarkShadowsMode.Main) {
                UpdateShadowsOverride(holder, shadowsPosition, shadowsDraw);
            }
        }

        protected abstract void SetShadowOverride(out Vector4 size, out Matrix matrix, out ShaderResourceView view, ref Vector4 nearFar);

        private RendererStopwatch _stopwatch;
        private float _smoothPosition;

        protected virtual void SetOverride(IDeviceContextHolder holder, ref EffectDarkMaterial.Light light) {
            var brightnessMultipler = Brightness * BrightnessMultiplier;

            if (_smoothChanging && _smoothDelay.HasValue) {
                if (_stopwatch == null) {
                    _stopwatch = holder.StartNewStopwatch();
                }

                var delta = _stopwatch.ElapsedSeconds / _smoothDelay.Value.TotalSeconds;
                _stopwatch.Reset();

                _smoothPosition = (float)(Enabled ? _smoothPosition + delta : _smoothPosition - delta).Saturate();
                brightnessMultipler *= _smoothPosition.SmoothStep();

                if (_smoothPosition == (Enabled ? 1f : 0f)) {
                    _smoothChanging = false;
                    _stopwatch = null;
                }
            }
            
            light.PosW = ActualPosition;
            light.Color = Color.ToVector3() * brightnessMultipler;
            light.ShadowMode = _shadowsOffset == -1 ? 0 : (uint)_shadowsMode;
            light.ShadowCube = false;
            light.ShadowId = (uint)_shadowsOffset;
        }

        // don’t need to dispose anything here — those buffers don’t actually store anything, but only used for moving stuff to shader
        private static EffectDarkMaterial.Light[] _lightsBuffer;
        private static Vector4[] _extraShadowsSizesBuffer;
        private static Vector4[] _extraShadowsNearFarBuffer;
        private static Matrix[] _extraShadowsMatricesBuffer;
        private static ShaderResourceView[] _extraShadowsViewsBuffer;

        private static void ResizeBuffers(int lightsCount, int shadowsCount) {
            if (_lightsBuffer?.Length != lightsCount) {
                _lightsBuffer = new EffectDarkMaterial.Light[lightsCount];
            }

            if (_extraShadowsSizesBuffer?.Length != shadowsCount) {
                _extraShadowsSizesBuffer = new Vector4[shadowsCount];
                _extraShadowsNearFarBuffer = new Vector4[shadowsCount];
                _extraShadowsMatricesBuffer = new Matrix[shadowsCount];
                _extraShadowsViewsBuffer = new ShaderResourceView[shadowsCount];
            }
        }

        public static void FlipPreviousY(EffectDarkMaterial effect) {
            for (var i = 0; i < _lightsBuffer.Length; i++) {
                _lightsBuffer[i].DirectionW.Y *= -1f;
                _lightsBuffer[i].PosW.Y *= -1f;
            }

            effect.FxLights.SetArray(_lightsBuffer);
        }

        public static void ToShader(IDeviceContextHolder holder, EffectDarkMaterial effect, DarkLightBase[] lights, int count, int shadowsCount) {
            ResizeBuffers(EffectDarkMaterial.MaxLighsAmount, shadowsCount);

            var shadowOffset = 0;

            for (var i = count - 1; i >= 0; i--) {
                var l = lights[i];
                if (l.ActuallyEnabled && l._shadowsMode != DarkShadowsMode.Off && l._shadowsMode != DarkShadowsMode.Main) {
                    lights[i]._shadowsOffset = -1;
                }
            }

            for (var i = 0; i < count; i++) {
                var l = lights[i];
                if (l.ActuallyEnabled && l.UseShadows && l._shadowsMode == DarkShadowsMode.ExtraSmooth) {
                    var offset = shadowOffset++;
                    if (offset < shadowsCount) {
                        l.SetShadowOverride(out _extraShadowsSizesBuffer[offset], out _extraShadowsMatricesBuffer[offset],
                                out _extraShadowsViewsBuffer[offset], ref _extraShadowsNearFarBuffer[offset]);
                        if (offset >= EffectDarkMaterial.MaxExtraShadowsSmooth) {
                            l.UseHighQualityShadows = false;
                        }

                        l._shadowsOffset = offset;
                    } else {
                        l._shadowsOffset = -1;
                    }
                }
            }

            for (var i = 0; i < count; i++) {
                var l = lights[i];
                if (l.ActuallyEnabled && l.UseShadows && l._shadowsMode == DarkShadowsMode.ExtraFast && l._shadowsOffset == -1) {
                    var offset = shadowOffset++;
                    if (offset < shadowsCount) {
                        l.SetShadowOverride(out _extraShadowsSizesBuffer[offset], out _extraShadowsMatricesBuffer[offset],
                                out _extraShadowsViewsBuffer[offset], ref _extraShadowsNearFarBuffer[offset]);
                        l._shadowsOffset = offset;
                    } else {
                        l._shadowsOffset = -1;
                    }
                }
            }

            var j = 0;
            for (var i = 0; i < count && j < EffectDarkMaterial.MaxLighsAmount; i++) {
                var l = lights[i];
                if (l.ActuallyEnabled) {
                    l.SetOverride(holder, ref _lightsBuffer[j++]);
                }
            }

            for (; j < EffectDarkMaterial.MaxLighsAmount; j++) {
                _lightsBuffer[j].Type = EffectDarkMaterial.LightOff;
            }

            effect.FxLights.SetArray(_lightsBuffer);

            if (effect.FxExtraShadowMapSize.IsValid) {
                effect.FxExtraShadowMapSize.Set(_extraShadowsSizesBuffer);
                effect.FxExtraShadowViewProj.SetMatrixArray(_extraShadowsMatricesBuffer);
                effect.FxExtraShadowMaps.SetResourceArray(_extraShadowsViewsBuffer);
                effect.FxExtraShadowNearFar.Set(_extraShadowsNearFarBuffer);
            }
        }

        public abstract void InvalidateShadows();

        public abstract void DrawDummy(IDeviceContextHolder holder, ICamera camera);

        public void Dispose() {
            DisposeOverride();
            DisposeHelper.Dispose(ref _debugText);
        }

        protected abstract void DisposeOverride();

        #region Tag
        private DarkLightTag _tag = DarkLightTag.Extra;

        public DarkLightTag Tag {
            get { return _tag; }
            set {
                if (value == _tag) return;
                _tag = value;
                UpdateActs();
                OnPropertyChanged();
                BrightnessMultiplier = 1f;
                AttachedToObject = null;
            }
        }
        #endregion

        #region Car light
        private bool _actAsHeadlight;

        public bool ActAsHeadlight {
            get { return _actAsHeadlight; }
            private set {
                if (value == _actAsHeadlight) return;
                _actAsHeadlight = value;
                OnPropertyChanged();
            }
        }

        private bool _actAsBrakeLight;

        public bool ActAsBrakeLight {
            get { return _actAsBrakeLight; }
            private set {
                if (value == _actAsBrakeLight) return;
                _actAsBrakeLight = value;
                OnPropertyChanged();
            }
        }

        public float BrightnessMultiplier { get; set; } = 1f;

        // TODO: raise amount of shadows in one more sets?
        // TODO: don’t actually disable shadows is there is not enough slots

        private void UpdateActs() {
            var name = DisplayName;
            if (!Tag.IsCarTag || name == null) return;
            
            if (name.Contains("brake", StringComparison.OrdinalIgnoreCase)) {
                ActAsBrakeLight = true;
                ActAsHeadlight = false;
            } else {
                ActAsHeadlight = true;
                ActAsBrakeLight = false;
            }
        }
        #endregion

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

            DrawText($"{DisplayName} ({Tag})", Matrix.Translation(ActualPosition), camera, screenSize,
                    Enabled ? new Color4(1f, 1f, 1f, 0f) : new Color4(1f, 0.4f, 0.4f, 0f));
        }
        #endregion
    }
}