using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using AcTools.Render.Base;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Data;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Render.Kn5SpecificForwardDark.Lights;
using AcTools.Render.Shaders;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;
using SlimDX;
using SlimDX.Direct3D11;

namespace AcTools.Render.Kn5SpecificForwardDark {
    public interface IDarkLightsDescriptionProvider {
        [CanBeNull]
        string GetFilename([NotNull] string id);
    }

    public partial class DarkKn5ObjectRenderer {
        private DarkDirectionalLight _mainLight, _reflectedLight;

        private void InitializeLights() {
            _mainLight = new DarkDirectionalLight {
                Tag = DarkLightTag.Main,
                DisplayName = "Sun",
                Position = Vector3.UnitY - Vector3.UnitZ,
                IsMainLightSource = true,
                UseShadows = false,
                IsVisibleInUi = false,
                IsMovable = false
            };

            _reflectedLight = new DarkDirectionalLight {
                Tag = DarkLightTag.Main,
                DisplayName = "Sun (Reflected)",
                Position = -Vector3.UnitY - Vector3.UnitZ,
                Enabled = false,
                IsMovable = false,
                IsVisibleInUi = false,
                UseShadows = true,
                UseHighQualityShadows = true
            };

            AddLight(_mainLight);
            AddLight(_reflectedLight);
        }

        #region Guessing car lights
        private readonly Dictionary<int, bool> _lightsGuessed = new Dictionary<int, bool>();
        private bool _tryToGuessCarLightsIfMissing;

        public bool TryToGuessCarLightsIfMissing {
            get { return _tryToGuessCarLightsIfMissing; }
            set {
                if (Equals(value, _tryToGuessCarLightsIfMissing)) return;
                _tryToGuessCarLightsIfMissing = value;
                OnPropertyChanged();

                if (value) {
                    foreach (var slot in CarSlots) {
                        var tag = DarkLightTag.GetCarTag(slot.Id);
                        if (Lights.All(x => x.Tag != tag) && slot.CarNode != null) {
                            TryToGuessCarLights(tag, slot.CarNode);
                        }
                    }
                } else {
                    foreach (var id in _lightsGuessed.Keys) {
                        RemoveLights(DarkLightTag.GetCarTag(id));
                    }
                }
            }
        }

        [CanBeNull]
        private IDarkLightsDescriptionProvider _lightsDescriptionProvider;

        public void SetLightsDescriptionProvider([CanBeNull] IDarkLightsDescriptionProvider provider) {
            _lightsDescriptionProvider = provider;
        }

        private static float IsHeadlightColor(Vector3? color) {
            if (color == null) return 0f;

            var v = color.Value;
            var n = Vector3.Normalize(v);
            var c = n.ToDrawingColor();
            var saturation = c.GetSaturation();
            var brightness = c.GetBrightness();
            var hue = c.GetHue();

            if (saturation > 0.2 && (hue < 20 || hue > 340)) return 0f;
            if (saturation > 0.8) return 0f;
            if (brightness < 0.4) return 0f;
            if (v.Length() < 30) return 0f;

            return v.Length() * (2f - saturation);
        }

        private static float IsBrakeLightColor(Vector3? color) {
            if (color == null) return 0f;

            var v = color.Value;
            var n = Vector3.Normalize(v);
            var c = n.ToDrawingColor();
            var saturation = c.GetSaturation();
            var hue = c.GetHue();

            if (hue > 40 && hue < 320) return 0f;
            if (saturation < 0.4) return 0f;
            if (v.Length() < 5) return 0f;

            return v.Length() * (1f + saturation);
        }

        private class InnerPair {
            public Vector3 Position;
            public Vector3 Normal;
            public int Count;
            public BoundingBox BoundingBox;
        }

        private int CreateLightsForEmissiveMesh(DarkLightTag tag, CarData.LightObject light, Kn5RenderableObject mesh) {
            if (mesh?.BoundingBox.HasValue != true) return 0;

            var inv = Matrix.Invert(Matrix.Transpose(mesh.ParentMatrix));

            var vertices = mesh.Vertices.Select(x => new {
                Position = Vector3.TransformCoordinate(x.Position, mesh.ParentMatrix),
                Normal = Vector3.TransformCoordinate(x.Normal, inv)
            }).ToArray();
            var list = new List<InnerPair>();

            var threshold = 0.3f;
            foreach (var v in vertices) {
                var min = float.PositiveInfinity;
                InnerPair minPair = null;
                foreach (var t in list) {
                    if (BoundingBox.Contains(t.BoundingBox, v.Position) == ContainmentType.Contains) {
                        minPair = t;
                        break;
                    }

                    var d = (v.Position - t.Position).LengthSquared();
                    if (d > min || d > threshold) continue;
                    min = d;
                    minPair = t;
                }

                if (minPair == null) {
                    list.Add(new InnerPair {
                        Position = v.Position,
                        Normal = v.Normal,
                        Count = 1,
                        BoundingBox = new BoundingBox(v.Position, v.Position)
                    });
                } else {
                    minPair.Position = (minPair.Position * minPair.Count + v.Position) / (1f + minPair.Count);
                    minPair.Normal = (minPair.Normal * minPair.Count + v.Normal) / (1f + minPair.Count);
                    minPair.Position.ExtendBoundingBox(ref minPair.BoundingBox);
                    minPair.Count++;
                }
            }

            for (var i = 1; i < list.Count; i++) {
                var v = list[i];
                for (var j = 0; j < i; j++) {
                    var t = list[j];
                    var d = (v.Position - t.Position).LengthSquared();
                    if (d <= threshold) {
                        v.Position = (v.Position * v.Count + t.Position * t.Count) / (v.Count + t.Count);
                        v.Normal = (v.Normal * v.Count + t.Normal * t.Count) / (v.Count + t.Count);
                        v.Count += t.Count;
                        t.BoundingBox.ExtendBoundingBox(ref v.BoundingBox);

                        // list.Remove(v); // wut? why?
                        list.Remove(t);

                        i = 0;
                        break;
                    }
                }
            }

            var result = 0;
            if (light.BrakeColor != null) {
                var emissive = light.BrakeColor.Value;
                foreach (var x in list) {
                    AddLight(new DarkSpotLight {
                        DisplayName = $"Brakelight {"RML"[x.Position.X.Sign() + 1]}",
                        AsHeadlightMultiplier = light.HeadlightColor?.Length() / emissive.Length() ?? 0f,
                        Tag = tag,
                        Position = x.Position + x.Normal * 0.1f,
                        Direction = Vector3.Normalize(x.Normal + new Vector3(0f, -1f, -1f)),
                        Range = 1.5f,
                        Angle = 1.4f,
                        Color = Vector3.Normalize(emissive).ToDrawingColor(),
                        UseShadows = true,
                        Brightness = (emissive.Length() * x.BoundingBox.GetSize().Length() * 0.2f).Clamp(0.2f, 0.5f)
                    });
                    result++;
                }
            } else {
                var emissive = light.HeadlightColor ?? default(Vector3);
                if (emissive == default(Vector3)) return 0;

                foreach (var x in list) {
                    AddLight(new DarkSpotLight {
                        DisplayName = $"Headlight {"RML"[x.Position.X.Sign() + 1]}",
                        Tag = tag,
                        Position = x.Position + x.Normal * 0.05f,
                        Direction = new Vector3(0, -0.45f, 0.9f),
                        Range = 13.5f,
                        Color = Vector3.Normalize(emissive).ToDrawingColor(),
                        UseShadows = true,
                        Brightness = (emissive.Length() * x.BoundingBox.GetSize().Length() * 0.2f).Clamp(2.0f, 3.5f)
                    });
                    result++;
                }
            }

            return result;
        }

        private bool TryToGuessCarLights(DarkLightTag tag, [NotNull] Kn5RenderableCar car) {
            // unline with deferred renderer, here we only try to guess four lights, two headlights and two rearlights, 
            // which should be symmetrical with proper colors and all that

            var lights = car.GetCarLights().ToArrayIfItIsNot();

            const int limit = 2;
            var headlightsAdded = 0;
            var brakeLightsAdded = 0;

            foreach (var light in lights.Select(x => new {
                Priority = Math.Max(IsHeadlightColor(x.Description?.HeadlightColor), IsBrakeLightColor(x.Description?.BrakeColor)),
                Light = x
            }).Where(x => x.Priority > 0f).OrderBy(x => x.Priority)) {
                var isBrakeLight = light.Light.Description?.BrakeColor != null;
                if ((isBrakeLight ? brakeLightsAdded : headlightsAdded) >= limit) continue;

                var mesh = car.RootObject.GetAllChildren().OfType<Kn5RenderableObject>().FirstOrDefault(x => x.Name == light.Light.Description?.Name);
                if (mesh == null) continue;

                var added = CreateLightsForEmissiveMesh(tag, light.Light.Description, mesh);
                if (isBrakeLight) {
                    brakeLightsAdded += added;
                } else {
                    headlightsAdded += added;
                }
            }

            return headlightsAdded > 0 || brakeLightsAdded > 0;
        }
        #endregion

        #region Lights list, methods
        [NotNull]
        private DarkLightBase[] _lights = new DarkLightBase[0];

        [NotNull]
        public DarkLightBase[] Lights {
            get { return _lights; }
            set {
                if (Equals(value, _lights)) return;

                for (var i = _lights.Length - 1; i >= 0; i--) {
                    var light = _lights[i];
                    if (!value.Contains(light)) {
                        light.Dispose();
                        light.PropertyChanged -= OnLightPropertyChanged;
                    }
                }

                for (var i = value.Length - 1; i >= 0; i--) {
                    var light = value[i];
                    if (!_lights.Contains(light)) {
                        light.PropertyChanged += OnLightPropertyChanged;
                    }
                }

                _lights = value;
                OnPropertyChanged();
                IsDirty = true;
                SetReflectionCubemapDirty();
            }
        }

        private void OnLightPropertyChanged(object sender, PropertyChangedEventArgs e) {
            switch (e.PropertyName) {
                case nameof(DarkLightBase.Type):
                    var light = (DarkLightBase)sender;
                    RemoveLight(light);

                    if (light.Tag == DarkLightTag.Extra) {
                        AddLight(light.ChangeType(light.Type));
                    } else {
                        var index = _lights.FindIndex(x => x.Tag > light.Tag);
                        if (index == -1) {
                            AddLight(light.ChangeType(light.Type));
                        } else {
                            InsertLightAt(light.ChangeType(light.Type), index);
                        }
                    }
                    break;
                case nameof(DarkLightBase.IsDeleted):
                    RemoveLight((DarkLightBase)sender);
                    break;
                default:
                    if (!Disposed) {
                        LightPropertyChanged?.Invoke(sender, e);
                    }
                    break;
            }
        }

        public event PropertyChangedEventHandler LightPropertyChanged;

        private void PrepareNewLight(DarkLightBase light) {
            if (light.DisplayName == null) {
                for (var i = 1; i < 999; i++) {
                    var c = "Light #" + i;
                    if (_lights.All(x => x.DisplayName != c)) {
                        light.DisplayName = c;
                        break;
                    }
                }
            }
        }

        public void AddLight(DarkLightBase light) {
            PrepareNewLight(light);
            var updated = new DarkLightBase[_lights.Length + 1];
            Array.Copy(_lights, updated, _lights.Length);
            updated[updated.Length - 1] = light;
            Lights = updated;
        }

        public void InsertLightAt(DarkLightBase light, int index) {
            PrepareNewLight(light);
            var updated = new DarkLightBase[_lights.Length + 1];
            Array.Copy(_lights, updated, index);
            Array.Copy(_lights, index, updated, index + 1, _lights.Length - index);
            updated[index] = light;
            Lights = updated;
        }

        public void RemoveLight(DarkLightBase light) {
            var index = _lights.IndexOf(light);
            if (index == -1) return;

            var updated = new DarkLightBase[_lights.Length - 1];
            Array.Copy(_lights, updated, index);
            Array.Copy(_lights, index + 1, updated, index, updated.Length - index);
            Lights = updated;
        }

        public void RemoveLights(DarkLightTag tag) {
            if (Lights.Any(x => x.Tag == tag)) {
                Lights = Lights.Where(x => x.Tag != tag).ToArray();
            }
        }
        #endregion

        #region Loading and saving
        public JObject[] SerializeLights(DarkLightTag tag) {
            return Lights.Where(x => x.Tag == tag).Select(x => x.SerializeToJObject()).NonNull().ToArray();
        }

        public void DeserializeLights(DarkLightTag tag, [CanBeNull] IEnumerable<JObject> data) {
            var deserialized = data?.Select(DarkLightBase.Deserialize).NonNull().ToArray() ?? new DarkLightBase[0];
            if (deserialized.Length == 0) {
                RemoveLights(tag);
                return;
            }

            foreach (var light in deserialized) {
                light.Tag = tag;
            }

            // keep the right order
            Lights = Lights.Where(x => x.Tag < tag)
                           .Concat(deserialized)
                           .Concat(Lights.Where(x => x.Tag > tag))
                           .ToArray();
        }

        public bool LoadObjLights(DarkLightTag tag, string objDirectory) {
            try {
                var filename = Path.Combine(objDirectory, "ui", "cm_lights.json");
                if (!File.Exists(filename)) {
                    filename = _lightsDescriptionProvider?.GetFilename(Path.GetFileName(objDirectory) ?? "");
                    if (filename == null || !File.Exists(filename)) {
                        RemoveLights(tag);
                        return false;
                    }
                }

                var lights = JArray.Parse(File.ReadAllText(filename)).OfType<JObject>();
                DeserializeLights(tag, lights);
                return true;
            } catch (Exception e) {
                AcToolsLogging.Write(e);
                RemoveLights(tag);
                return false;
            }
        }
        #endregion

        #region Quickly add or remove extra lights
        public void AddLight() {
            var color = new Vector3((float)MathUtils.Random(), (float)MathUtils.Random(),
                    (float)MathUtils.Random());
            color.Normalize();

            /*AddLight(new DarkPointLight {
                UseShadows = true,
                UseHighQualityShadows = true,
                Color = color.ToDrawingColor(),
                Range = 20f,
                Position = Vector3.UnitY * 2f,
                Brightness = 5.5f
            });*/

            AddLight(new DarkAreaTubeLight {
                UseShadows = true,
                UseHighQualityShadows = true,
                Color = color.ToDrawingColor(),
                Range = 20f,
                Position = Vector3.UnitY * 2f,
                Brightness = 5.5f
            });
        }

        public void RemoveLight() {
            var last = _lights.LastOrDefault(x => x.Tag == DarkLightTag.Extra);
            if (last == null || _movingLights.Any(x => x.Light == last)) return;

            RemoveLight(last);
        }
        #endregion

        #region Moving lights
        private class MovingLight {
            public MovingLight(IDeviceContextHolder holder) {
                var color = new Vector3(MathUtils.Random(1f), MathUtils.Random(1f),
                        MathUtils.Random(1f));
                color.Normalize();

                /*Light = new DarkSpotLight {
                    Tag = DarkLightTag.Main,
                    UseShadows = true,
                    Color = color.ToDrawingColor(),
                    Range = 10f,
                    Angle = 0.5f,
                    Brightness = 0.5f,
                    SpotFocus = 0.75f,
                    IsMovable = false
                };*/

                Light = new DarkSpotLight {
                    Tag = DarkLightTag.Main,
                    UseShadows = true,
                    UseHighQualityShadows = true,
                    ShadowsResolution = 2048,
                    Color = color.ToDrawingColor(),
                    Range = 30f,
                    Angle = 0.5f,
                    Brightness = 0.5f,
                    SpotFocus = 0.75f,
                    IsMovable = false
                };

                _a = MathUtils.Random(2f, 3f);
                _b = MathUtils.Random(1f);
                _c = MathUtils.Random(5f, 6f);
                _d = MathUtils.Random(1f);
                _e = MathUtils.Random(1f);
                _f = MathUtils.Random(1f);
                _g = MathUtils.Random(3f, 4f);

                _stopwatch = holder.StartNewStopwatch();
            }

            public readonly DarkLightBase Light;
            private readonly RendererStopwatch _stopwatch;
            private readonly float _a, _b, _c, _d, _e, _f, _g;

            public bool Update() {
                if (!Light.Enabled) return false;

                var elapsed = (float)_stopwatch.ElapsedSeconds;
                Light.Position = new Vector3(
                        (elapsed * _b + _d).Sin() * _c, _a,
                        (elapsed * _e + _f).Sin() * _g);

                var spot = Light as DarkSpotLight;
                if (spot != null) {
                    spot.Direction = -spot.Position;
                }

                return true;
            }
        }

        private readonly List<MovingLight> _movingLights = new List<MovingLight>();

        public void AddMovingLight() {
#if DEBUG
            var moving = new MovingLight(DeviceContextHolder);
            AddLight(moving.Light);
            _movingLights.Add(moving);
#endif
        }

        public void RemoveMovingLight() {
#if DEBUG
            var last = _movingLights.LastOrDefault();
            if (last == null) return;

            RemoveLight(last.Light);
            _movingLights.Remove(last);
#endif
        }
        #endregion

        #region Car and showroom lights (auto-loading)
        private void OnCarChangedLights([NotNull] CarSlot slot, [CanBeNull] Kn5RenderableCar car) {
            var lightTag = DarkLightTag.GetCarTag(slot.Id);
            RemoveLights(lightTag);

            if (car != null && !LoadObjLights(lightTag, car.RootDirectory) &&
                    TryToGuessCarLightsIfMissing) {
                TryToGuessCarLights(lightTag, car);
                _lightsGuessed[slot.Id] = true;
            } else {
                _lightsGuessed.Remove(slot.Id);
            }
        }

        private void OnShowroomChangedLights() {
            if (ShowroomNode == null) {
                RemoveLights(DarkLightTag.Showroom);
            } else {
                LoadObjLights(DarkLightTag.Showroom, ShowroomNode.RootDirectory);
            }
        }
        #endregion

        #region Reflected light
        private bool IsFlatMirrorReflectedLightEnabled => FlatMirrorReflectedLight && ShowroomNode == null && FlatMirror && !FlatMirrorBlurred;

        private void UpdateReflectedLightShadowSize(float shadowSize) {
            _reflectedLight.ShadowsSize = shadowSize;
        }
        #endregion

        #region Update methods
        private void UpdateLights(Vector3 mainLightDirection, bool setShadows, bool singleLight) {
            var effect = Effect;

            _mainLight.Direction = -mainLightDirection;
            _mainLight.Brightness = LightBrightness;
            _mainLight.Color = LightColor;

            if (IsFlatMirrorReflectedLightEnabled) {
                _reflectedLight.Direction = new Vector3(-mainLightDirection.X, mainLightDirection.Y, -mainLightDirection.Z);
                _reflectedLight.Brightness = LightBrightness * FlatMirrorReflectiveness;
                _reflectedLight.Color = LightColor;
                _reflectedLight.ShadowsResolution = ShadowMapSize;
                _reflectedLight.Enabled = true;
            } else {
                _reflectedLight.Enabled = false;
            }

            for (var i = _lights.Length - 1; i >= 0; i--) {
                var l = _lights[i];
                l.Update(DeviceContextHolder, ShadowsPosition, setShadows && !singleLight && EnableShadows ? this : null);
            }

            SetEffectNoiseMap();
            DarkLightBase.ToShader(DeviceContextHolder, effect, _lights, singleLight ? 1 : _lights.Length,
                    _limitedMode ? EffectDarkMaterial.MaxExtraShadowsFewer : EffectDarkMaterial.MaxExtraShadows);
        }

        private static void UpdateCarLights(CarSlot slot, DarkLightBase[] lights) {
            var car = slot.CarNode;
            var tag = DarkLightTag.GetCarTag(slot.Id);
            if (car == null) {
                for (var i = lights.Length - 1; i >= 0; i--) {
                    var l = lights[i];
                    if (l.Tag != tag) continue;

                    l.Enabled = false;
                }
            } else {
                for (var i = lights.Length - 1; i >= 0; i--) {
                    var l = lights[i];
                    if (l.Tag != tag) continue;

                    if (l.ActAsHeadlight) {
                        l.Enabled = car.HeadlightsEnabled;
                        l.BrightnessMultiplier = 1f;

                        if (!l.SmoothDelay.HasValue) {
                            l.SmoothDelay = car.GetApproximateHeadlightsDelay() ?? TimeSpan.Zero;
                        }
                    } else if (l.ActAsBrakeLight) {
                        if (l.AsHeadlightMultiplier == 0f) {
                            l.Enabled = car.BrakeLightsEnabled;
                            l.BrightnessMultiplier = 1f;
                        } else {
                            l.Enabled = car.BrakeLightsEnabled || car.HeadlightsEnabled;
                            l.BrightnessMultiplier = car.BrakeLightsEnabled ? 1f : l.AsHeadlightMultiplier;
                        }

                        if (!l.SmoothDelay.HasValue) {
                            l.SmoothDelay = car.GetApproximateBrakeLightsDelay() ?? TimeSpan.Zero;
                        }
                    }

                    if (l.AttachedTo == null) {
                        l.ParentMatrix = car.Matrix;
                    } else {
                        if (l.AttachedToObject == null) {
                            l.AttachedToObject = car.GetByName(l.AttachedTo) ?? (IRenderableObject)car.RootObject;
                            l.AttachedToRelativeMatrix = Matrix.Invert(FindOriginalMatrix(l.AttachedToObject, car.RootObject, Matrix.Identity));
                            if (l.AttachedToObject == null) continue;
                        }

                        var a = l.AttachedToObject as Kn5RenderableList;
                        if (a == null) {
                            l.ParentMatrix = l.AttachedToRelativeMatrix * l.AttachedToObject.ParentMatrix;
                        } else {
                            l.ParentMatrix = l.AttachedToRelativeMatrix * a.Matrix;
                        }
                    }
                }
            }
        }

        private static Matrix FindOriginalMatrix(IRenderableObject obj, RenderableList root, Matrix matrix) {
            while (obj != root && obj != null) {
                matrix *= (obj as Kn5RenderableList)?.OriginalNode.Transform.ToMatrix() ?? (obj as RenderableList)?.LocalMatrix ?? Matrix.Identity;
                obj = obj.GetParent(root);
            }

            return matrix;
        }

        private void UpdateCarLights() {
            for (var i = CarSlots.Length - 1; i >= 0; i--) {
                UpdateCarLights(CarSlots[i], _lights);
            }
        }

        private void InvalidateLightsShadows() {
            if (_complexMode) {
                for (var i = _lights.Length - 1; i >= 0; i--) {
                    _lights[i].InvalidateShadows();
                }
            }
        }
        #endregion

        #region Dark shaders modes
        private bool _complexMode;
        private bool _limitedMode;
        private EffectDarkMaterial.Mode _darkMode;

        private EffectDarkMaterial.Mode FindAppropriateMode() {
            if (IsInDebugMode()) {
                _complexMode = true;
                _limitedMode = false;
                return EffectDarkMaterial.Mode.Main;
            }

            var useComplex = false;
            var areaLights = false;

            for (var i = 0; i < _lights.Length; i++) {
                var light = _lights[i];
                if (light.ActuallyEnabled) {
                    useComplex = true;
                    if (!light.ShadowsAvailable) areaLights = true;
                }
            }

            _complexMode = useComplex || IsFlatMirrorReflectedLightEnabled;

            if (areaLights) {
                return EffectDarkMaterial.Mode.Main;
            }

            if (!EnableShadows) {
                return useComplex ? EffectDarkMaterial.Mode.NoExtraShadows : EffectDarkMaterial.Mode.SimpleNoShadows;
            }

            if (!useComplex) {
                if (LightBrightness <= 0f || LightColor == Color.Black) {
                    return EffectDarkMaterial.Mode.WithoutLighting;
                }

                return UsePcss ? EffectDarkMaterial.Mode.Simple : EffectDarkMaterial.Mode.SimpleNoPCSS;
            }

            var shadowsAmount = _lights.Count(x => x.ActuallyEnabled && x.UseShadows);
            _limitedMode = shadowsAmount <= EffectDarkMaterial.MaxExtraShadowsFewer;

            return _limitedMode
                    ? (UsePcss ? EffectDarkMaterial.Mode.FewerExtraShadows : EffectDarkMaterial.Mode.FewerExtraShadowsNoPCSS)
                    : (UsePcss ? EffectDarkMaterial.Mode.NoAreaLights : EffectDarkMaterial.Mode.NoPCSS);
        }

        private void UpdateEffect() {
            if (_effect == null) {
                _effect = DeviceContextHolder.GetExistingEffect<EffectDarkMaterial>();
            }

            _darkMode = FindAppropriateMode();
            if (_effect == null) {
                _effect = DeviceContextHolder.GetEffect<EffectDarkMaterial>(e => e.SetMode(_darkMode, Device));
            } else if (_effect.GetMode() != _darkMode) {
                _effect.SetMode(_darkMode, Device);
                _pcssParamsSet = false;
                _effectNoiseMapSet = false;
                SetShadowsDirty();
                SetReflectionCubemapDirty();
                // TODO: more?
            } else {
                return;
            }

            if (_darkMode == EffectDarkMaterial.Mode.Main) {
                InitializeAreaLightsTextures();
            }
        }

        private ShaderResourceView[] _ltcViews;

        private void InitializeAreaLightsTextures() {
            DisposeHelper.Dispose(ref _ltcViews);

            _ltcViews = new[] {
                ShaderResourceView.FromMemory(Device, Resources.LtcMat),
                ShaderResourceView.FromMemory(Device, Resources.LtcAmp)
            };

            _effect.FxLtcMap.SetResource(_ltcViews[0]);
            _effect.FxLtcAmp.SetResource(_ltcViews[1]);
        }
        #endregion

        private void DisposeLights() {
            Lights = new DarkLightBase[0]; // thus, disposing everything
            DisposeHelper.Dispose(ref _ltcViews);
        }
    }
}