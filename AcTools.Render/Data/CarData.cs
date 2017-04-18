using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using AcTools.DataFile;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Objects;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Data {
    internal static class IniExtension {
        public static Vector3 GetSlimVector3(this IniFileSection section, string key, Vector3 defaultValue = default(Vector3)) {
            return section.GetVector3F(key).ToVector3();
        }
    }

    public class CarData {
        [NotNull]
        private readonly DataWrapper _data;

        [NotNull]
        public string CarDirectory { get; }

        public CarData([NotNull] string carDirectory) {
            _data = DataWrapper.FromDirectory(carDirectory);
            CarDirectory = carDirectory;
        }

        public CarData([NotNull] DataWrapper data) {
            _data = data;
            CarDirectory = data.ParentDirectory;
        }

        public bool IsEmpty => _data.IsEmpty;

        public string GetMainKn5(string carDirectory) {
            return FileUtils.GetMainCarFilename(carDirectory, _data);
        }

        #region Ambient shadows
        public Vector3 GetBodyShadowSize() {
            var iniFile = _data.GetIniFile("ambient_shadows.ini");
            return new Vector3(
                    (float)iniFile["SETTINGS"].GetDouble("WIDTH", 1d), 1.0f,
                    (float)iniFile["SETTINGS"].GetDouble("LENGTH", 1d));
        }

        public Vector3 GetWheelShadowSize() {
            return new Vector3(0.3f, 1.0f, 0.3f);
        }
        #endregion

        #region Doors
        public AnimationBase GetLeftDoorAnimation() {
            return new AnimationBase("car_door_L.ksanim", 1f);
        }

        public AnimationBase GetRightDoorAnimation() {
            return new AnimationBase("car_door_R.ksanim", 1f);
        }
        #endregion

        #region Mirrors
        public IEnumerable<string> GetMirrorsNames() {
            return IsEmpty ? new string[0] :
                    _data.GetIniFile("mirrors.ini").GetSections("MIRROR").Select(section => section.GetNonEmpty("NAME")).Where(x => x != null);
        }
        #endregion

        #region Animation base
        public class AnimationBase {
            public string KsAnimName { get; }

            public float Duration { get; }

            public AnimationBase(string ksAnimName, float duration) {
                KsAnimName = ksAnimName;
                Duration = duration;
            }

            private sealed class KsAnimNameEqualityComparer : IEqualityComparer<AnimationBase> {
                public bool Equals(AnimationBase x, AnimationBase y) {
                    if (ReferenceEquals(x, y)) return true;
                    if (ReferenceEquals(x, null)) return false;
                    if (ReferenceEquals(y, null)) return false;
                    if (x.GetType() != y.GetType()) return false;
                    return string.Equals(x.KsAnimName, y.KsAnimName);
                }

                public int GetHashCode(AnimationBase obj) {
                    return obj.KsAnimName?.GetHashCode() ?? 0;
                }
            }

            public static IEqualityComparer<AnimationBase> KsAnimNameComparer { get; } = new KsAnimNameEqualityComparer();
        }
        #endregion

        #region Lights
        public class LightObject {
            public string Name { get; }

            public Vector3 HeadlightColor { get; }

            public Vector3 BrakeColor { get; }

            public LightObject(string name, Vector3 headlightColor, Vector3 brakeColor) {
                Name = name;
                HeadlightColor = headlightColor;
                BrakeColor = brakeColor;
            }
        }

        public class LightAnimation : AnimationBase {
            public LightAnimation(string ksAnimName, float duration) : base(ksAnimName, duration) {}
        }

        public IEnumerable<LightObject> GetLights() {
            if (IsEmpty) yield break;

            var ini = _data.GetIniFile("lights.ini");
            var supportsCombined = ini["HEADER"].GetInt("VERSION", 1) > 1;

            foreach (var x in ini.GetSections("BRAKE")) {
                var name = x.GetNonEmpty("NAME");
                if (name != null) {
                    yield return new LightObject(x.GetNonEmpty("NAME"), 
                        supportsCombined ? x.GetSlimVector3("OFF_COLOR") : default(Vector3),
                        x.GetSlimVector3("COLOR"));
                }
            }

            foreach (var x in ini.GetSections("LIGHT")) {
                var name = x.GetNonEmpty("NAME");
                if (name != null) {
                    yield return new LightObject(x.GetNonEmpty("NAME"),
                        x.GetSlimVector3("COLOR"),
                        default(Vector3));
                }
            }
        }

        [NotNull]
        public IEnumerable<LightAnimation> GetLightsAnimations() {
            return IsEmpty ? new LightAnimation[0] :
                    _data.GetIniFile("lights.ini")
                         .GetSections("LIGHT_ANIMATION")
                         .Select(x => new LightAnimation(x.GetNonEmpty("FILE"), x.GetFloat("TIME", 1f)))
                         .Where(x => x.KsAnimName != null)
                         .Append(new LightAnimation("lights.ksanim", 1f))
                         .Distinct<LightAnimation>(AnimationBase.KsAnimNameComparer);
        }
        #endregion

        #region Blurred objects
        public class BlurredObject {
            public BlurredObject(int wheelIndex, string staticName, string blurredName) {
                WheelIndex = wheelIndex;
                StaticName = staticName;
                BlurredName = blurredName;
            }

            public int WheelIndex { get; }

            public string StaticName { get; }

            public string BlurredName { get; }
        }

        public IEnumerable<BlurredObject> GetBlurredObjects() {
            return IsEmpty ? new BlurredObject[0] :
                    _data.GetIniFile("blurred_objects.ini").GetSections("OBJECT").GroupBy(x => x.GetInt("WHEEL_INDEX", -1))
                         .Select(x => new BlurredObject(
                                 x.First().GetInt("WHEEL_INDEX", -1),
                                 x.FirstOrDefault(y => y.GetInt("MIN_SPEED", 0) == 0)?.GetNonEmpty("NAME"),
                                 x.FirstOrDefault(y => y.GetInt("MIN_SPEED", 0) > 0)?.GetNonEmpty("NAME")
                                 )).Where(x => x.WheelIndex >= 0 && x.StaticName != null && x.BlurredName != null);
        }
        #endregion

        #region LODs
        public class LodDescription {
            public string FileName { get; }

            public float In { get; }

            public float Out { get; }

            internal LodDescription(IniFileSection fileSection) {
                FileName = fileSection.GetNonEmpty("FILE");
                In = fileSection.GetFloat("IN", 0f);
                Out = fileSection.GetFloat("OUT", 0f);
            }
        }

        public IEnumerable<LodDescription> GetLods() {
            return IsEmpty ? new LodDescription[0] :
                    _data.GetIniFile("lods.ini").GetSections("LOD").Select(x => new LodDescription(x)).Where(x => x.FileName != null);
        }
        #endregion

        #region Colliders
        public class ColliderDescription {
            public string Name { get; }

            public Vector3 Center { get; }

            public Vector3 Size { get; }

            public bool GroundEnable { get; }

            internal ColliderDescription(string name, IniFileSection fileSection) {
                Name = name;
                Center = fileSection.GetSlimVector3("CENTRE");
                Size = fileSection.GetSlimVector3("SIZE");
                GroundEnable = fileSection.GetBool("GROUND_ENABLE", true);
            }
        }

        public IEnumerable<ColliderDescription> GetColliders() {
            if (IsEmpty) {
                return new ColliderDescription[0];
            }

            var flames = _data.GetIniFile("colliders.ini");
            return flames.GetExistingSectionNames("COLLIDER").Select(x => new ColliderDescription(x, flames[x]));
        }
        #endregion

        #region Flames
        public class FlameDescription {
            public string Name { get; }

            public Vector3 Position { get; }

            public Vector3 Direction { get; }

            public int Group { get; }

            internal FlameDescription(string name, IniFileSection fileSection, Matrix graphicMatrix) {
                Name = name;
                Position = fileSection.GetSlimVector3("POSITION");
                Direction = fileSection.GetSlimVector3("DIRECTION");
                Group = fileSection.GetInt("GROUP", 0);
            }
        }

        public IEnumerable<FlameDescription> GetFlames() {
            if (IsEmpty) {
                return new FlameDescription[0];
            }

            var flames = _data.GetIniFile("flames.ini");
            var matrix = GetGraphicMatrix();
            return flames.GetExistingSectionNames("FLAME").Select(x => new FlameDescription(x, flames[x], matrix));
        }
        #endregion

        #region From car.ini: graphic offset, steer lock
        public Vector3 GetGraphicOffset() {
            return _data.GetIniFile("car.ini")["BASIC"].GetSlimVector3("GRAPHICS_OFFSET");
        }

        public Matrix GetGraphicMatrix() {
            var basic = _data.GetIniFile("car.ini")["BASIC"];
            return Matrix.Translation(basic.GetSlimVector3("GRAPHICS_OFFSET")) *
                    Matrix.RotationX(basic.GetFloat("GRAPHICS_PITCH_ROTATION", 0f).ToRadians());
        }

        public float GetSteerLock() {
            return _data.GetIniFile("car.ini")["CONTROLS"].GetFloat("STEER_LOCK", 180f);
        }
        #endregion

        #region Suspension
        public Vector3 GetCgOffset() {
            var suspensions = _data.GetIniFile("suspensions.ini");
            var basic = suspensions["BASIC"];
            var wheelbase = basic.GetFloat("WHEELBASE", 2f);
            var cgLocation = basic.GetFloat("CG_LOCATION", 0.5f);

            return GetGraphicOffset() + Vector3.UnitZ * (cgLocation - 0.5f) * wheelbase;
        }

        public Vector3 GetWheelGraphicOffset(string nodeName) {
            var suspensions = _data.GetIniFile("suspensions.ini");
            return new Vector3(-suspensions["GRAPHICS_OFFSETS"].GetFloat(nodeName, 0f), 0f, 0f);
        }

        public SuspensionsPack GetSuspensionsPack() {
            return SuspensionsPack.Create(_data);
        }

        public class SuspensionsPack : IEnumerable<SuspensionsGroupBase> {
            private SuspensionsPack(SuspensionsGroupBase front, SuspensionsGroupBase rear, Matrix graphicOffset) {
                Front = front;
                Rear = rear;
                GraphicOffset = graphicOffset;
            }

            public Matrix GraphicOffset { get; }

            [CanBeNull]
            public SuspensionsGroupBase Front { get; }

            [CanBeNull]
            public SuspensionsGroupBase Rear { get; }

            public Vector3 TranslateRelativeToCarModel([NotNull] SuspensionBase suspension, Vector3 point) {
                return Vector3.TransformCoordinate(point, TranslateRelativeToCarModel(suspension));
            }

            public Matrix TranslateRelativeToCarModel([NotNull] SuspensionBase suspension) {
                return Matrix.Translation(suspension.RefPoint) * GraphicOffset;
            }

            [NotNull]
            public static SuspensionsPack Create([NotNull] DataWrapper data) {
                var suspensions = data.GetIniFile("suspensions.ini");
                var car = data.GetIniFile("car.ini");
                var carBasic = car["BASIC"];
                var graphicOffset = Matrix.Translation(-carBasic.GetSlimVector3("GRAPHICS_OFFSET")) *
                        Matrix.RotationX(-carBasic.GetFloat("GRAPHICS_OFFSET", 0f));

                var tyres = data.GetIniFile("tyres.ini");
                return new SuspensionsPack(
                    SuspensionsGroupBase.Create(suspensions, true, tyres["FRONT"].GetFloat("RADIUS", 0f)),
                    SuspensionsGroupBase.Create(suspensions, false, tyres["REAR"].GetFloat("RADIUS", 0f)), graphicOffset);
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }

            public IEnumerator<SuspensionsGroupBase> GetEnumerator() {
                return new[] { Front, Rear }.OfType<SuspensionsGroupBase>().GetEnumerator();
            }
        }

        public abstract class SuspensionsGroupBase : INotifyPropertyChanged {
            public static SuspensionsGroupBase Create(IniFile ini, bool front, float wheelRadius) {
                var basic = ini["BASIC"];
                var section = ini[front ? "FRONT" : "REAR"];
                var type = section.GetNonEmpty("TYPE");
                switch (type) {
                    case "DWB":
                        return new IndependentSuspensionsGroup(
                                new DwbSuspension(basic, section, front, 1f, wheelRadius),
                                new DwbSuspension(basic, section, front, -1f, wheelRadius));

                    case "STRUT":
                        return new IndependentSuspensionsGroup(
                                new StrutSuspension(basic, section, front, 1f, wheelRadius),
                                new StrutSuspension(basic, section, front, -1f, wheelRadius));

                    case "AXLE":
                        return new DependentSuspensionGroup(
                                new AxleSuspension(basic, section, ini["AXLE"], front, wheelRadius));

                    default:
                        AcToolsLogging.Write($"Unknown suspension type: “{type}”");
                        return null;
                }
            }

            public abstract string Name { get; }

            public abstract string Kpi { get; }

            public abstract string Caster { get; }

            public event PropertyChangedEventHandler PropertyChanged {
                add { }
                remove { }
            }
        }

        public class IndependentSuspensionsGroup : SuspensionsGroupBase {
            public IndependentSuspensionsGroup(SuspensionBase left, SuspensionBase right) {
                Left = left;
                Right = right;
            }

            [NotNull]
            public SuspensionBase Left { get; }

            [NotNull]
            public SuspensionBase Right { get; }

            public override string Name => Left.DisplayType;

            public override string Kpi => Math.Abs(Left.Kpi - Right.Kpi) < 0.002 ? Left.Kpi.ToString("F3") :
                    $"{Left.Kpi:F3}/{Right.Kpi:F3}";

            public override string Caster => Math.Abs(Left.Caster - Right.Caster) < 0.002 ? Left.Caster.ToString("F3") :
                    $"{Left.Caster:F3}/{Right.Caster:F3}";
        }

        public class DependentSuspensionGroup : SuspensionsGroupBase {
            public DependentSuspensionGroup(SuspensionBase both) {
                Both = both;
            }

            [NotNull]
            public SuspensionBase Both { get; }

            public override string Name => Both.DisplayType;

            public override string Kpi => Both.Kpi.ToString("F3");

            public override string Caster => Both.Caster.ToString("F3");
        }

        public class DebugLine {
            public DebugLine(Color color, Vector3 start, Vector3 end) {
                Start = start;
                End = end;
                Color = color;
            }

            public Vector3 Start { get; }

            public Vector3 End { get; }

            public Color Color { get; }
        }

        public abstract class SuspensionBase {
            public bool Front { get; }

            public float WheelRadius { get; }

            public abstract string DisplayType { get; }

            protected SuspensionBase(bool front, float wheelRadius) {
                Front = front;
                WheelRadius = wheelRadius;
            }

            public Vector3 RefPoint { get; protected set; }

            public float StaticCamber { get; protected set; }

            #region Caster
            protected abstract float CasterOverride { get; }

            public float Caster => _caster ?? (_caster = CasterOverride).Value;
            private float? _caster;
            #endregion

            #region KPI
            protected abstract float KpiOverride { get; }

            public float Kpi => _kpi ?? (_kpi = KpiOverride).Value;
            private float? _kpi;
            #endregion

            #region Wheel steering axle
            protected abstract Tuple<Vector3, Vector3> WheelSteerAxisOverride { get; }

            public Tuple<Vector3, Vector3> WheelSteerAxis => _wheelSteerAxle ?? (_wheelSteerAxle = WheelSteerAxisOverride);
            private Tuple<Vector3, Vector3> _wheelSteerAxle;
            #endregion

            public DebugLine[] DebugLines => _debugLines ?? (_debugLines = DebugLinesOverride.ToArrayIfItIsNot());
            private DebugLine[] _debugLines;

            protected abstract IEnumerable<DebugLine> DebugLinesOverride { get; }
        }

        public class AxleLink {
            public AxleLink(Vector3 car, Vector3 axle) {
                Car = car;
                Axle = axle;
            }

            public Vector3 Car { get; }

            public Vector3 Axle { get; }
        }

        public class AxleSuspension : SuspensionBase {
            public float AxleWidth { get; }

            public AxleLink[] Links { get; }

            public override string DisplayType => "Axle";

            protected override float CasterOverride => 0f;

            protected override float KpiOverride => 0f;

            protected override Tuple<Vector3, Vector3> WheelSteerAxisOverride => Tuple.Create(
                new Vector3(AxleWidth / 2f, -1f, 0f), new Vector3(AxleWidth / 2f, 1f, 0f));

            public AxleSuspension(IniFileSection basic, IniFileSection section, IniFileSection axleSection, bool front, float wheelRadius) : base(front, wheelRadius) {
                var baseY = section.GetFloat("BASEY", 1f);
                var track = section.GetFloat("TRACK", 1f);
                var wheelbase = basic.GetFloat("WHEELBASE", 2f);
                var cgLocation = basic.GetFloat("CG_LOCATION", 0.5f);

                StaticCamber = basic.GetFloat("STATIC_CAMBER", 0f);
                RefPoint = front ?
                        new Vector3(0f, baseY, wheelbase * (1f - cgLocation)) :
                        new Vector3(0f, baseY, -wheelbase * cgLocation);

                AxleWidth = track;
                Links = Enumerable.Range(0, axleSection.GetInt("LINK_COUNT", 0)).Select(i => new AxleLink(
                        axleSection.GetSlimVector3($"J{i}_CAR"),
                        axleSection.GetSlimVector3($"J{i}_AXLE"))).ToArray();
            }

            protected override IEnumerable<DebugLine> DebugLinesOverride => Links.Select(x => new DebugLine(Color.Aqua, x.Car, x.Axle)).Append(
                new DebugLine(Color.White, new Vector3(-AxleWidth / 2f, 0f, 0f), new Vector3(AxleWidth / 2f, 0f, 0f)));
        }

        public abstract class EightPointsSuspensionBase : SuspensionBase {
            public Vector3[] Points { get; } = new Vector3[8];

            protected override float KpiOverride => ((Points[4].X - Points[5].X) / (Points[4].Y - Points[5].Y)).Atan() * 57.2957795f;

            public EightPointsSuspensionBase(bool front, float wheelRadius) : base(front, wheelRadius) {}
        }

        public class DwbSuspension : EightPointsSuspensionBase {
            public DwbSuspension(IniFileSection basic, IniFileSection section, bool front, float xOffset, float wheelRadius) : base(front, wheelRadius) {
                var baseY = section.GetFloat("BASEY", 1f);
                var track = section.GetFloat("TRACK", 1f);
                var wheelbase = basic.GetFloat("WHEELBASE", 2f);
                var cgLocation = basic.GetFloat("CG_LOCATION", 0.5f);

                StaticCamber = basic.GetFloat("STATIC_CAMBER", 0f);
                RefPoint = front ?
                        new Vector3(track * 0.5f * xOffset, baseY, wheelbase * (1f - cgLocation)) :
                        new Vector3(track * 0.5f * xOffset, baseY, -wheelbase * cgLocation);

                var vector3 = new Vector3(-xOffset, 1f, 1f);
                Points[0] = Vector3.Modulate(section.GetSlimVector3("WBCAR_TOP_FRONT"), vector3);
                Points[1] = Vector3.Modulate(section.GetSlimVector3("WBCAR_TOP_REAR"), vector3);
                Points[2] = Vector3.Modulate(section.GetSlimVector3("WBCAR_BOTTOM_FRONT"), vector3);
                Points[3] = Vector3.Modulate(section.GetSlimVector3("WBCAR_BOTTOM_REAR"), vector3);
                Points[4] = Vector3.Modulate(section.GetSlimVector3("WBTYRE_TOP"), vector3);
                Points[5] = Vector3.Modulate(section.GetSlimVector3("WBTYRE_BOTTOM"), vector3);
                Points[6] = Vector3.Modulate(section.GetSlimVector3("WBCAR_STEER"), vector3);
                Points[7] = Vector3.Modulate(section.GetSlimVector3("WBTYRE_STEER"), vector3);
            }

            public override string DisplayType => "DWB";

            protected override float CasterOverride => ((Points[4].Z - Points[5].Z) / (Points[4].Y - Points[5].Y)).Atan() * 57.2957795f;

            protected override Tuple<Vector3, Vector3> WheelSteerAxisOverride => new Tuple<Vector3, Vector3>(Points[5], Points[4]);

            protected override IEnumerable<DebugLine> DebugLinesOverride => new[] {
                new DebugLine(Color.Red, Points[0], Points[4]),
                new DebugLine(Color.Red, Points[1], Points[4]),
                new DebugLine(Color.Yellow, Points[2], Points[5]),
                new DebugLine(Color.Yellow, Points[3], Points[5]),
                new DebugLine(Color.Cyan, Points[6], Points[7]),
                new DebugLine(Color.Gray, Points[5], Points[4]),
            };
        }

        public class StrutSuspension : EightPointsSuspensionBase {
            public StrutSuspension(IniFileSection basic, IniFileSection section, bool front, float xOffset, float wheelRadius) : base(front, wheelRadius) {
                var baseY = section.GetFloat("BASEY", 1f);
                var track = section.GetFloat("TRACK", 1f);
                var wheelbase = basic.GetFloat("WHEELBASE", 2f);
                var cgLocation = basic.GetFloat("CG_LOCATION", 0.5f);

                StaticCamber = basic.GetFloat("STATIC_CAMBER", 0f);
                RefPoint = front ?
                        new Vector3(track * 0.5f * xOffset, baseY, wheelbase * (1f - cgLocation)) :
                        new Vector3(track * 0.5f * xOffset, baseY, -wheelbase * cgLocation);

                var vector3 = new Vector3(-xOffset, 1f, 1f);
                Points[0] = Vector3.Modulate(section.GetSlimVector3("STRUT_CAR"), vector3);
                Points[1] = Vector3.Modulate(section.GetSlimVector3("STRUT_TYRE"), vector3);
                Points[2] = Vector3.Modulate(section.GetSlimVector3("WBCAR_BOTTOM_FRONT"), vector3);
                Points[3] = Vector3.Modulate(section.GetSlimVector3("WBCAR_BOTTOM_REAR"), vector3);
                Points[5] = Vector3.Modulate(section.GetSlimVector3("WBTYRE_BOTTOM"), vector3);
                Points[6] = Vector3.Modulate(section.GetSlimVector3("WBCAR_STEER"), vector3);
                Points[7] = Vector3.Modulate(section.GetSlimVector3("WBTYRE_STEER"), vector3);
            }

            public override string DisplayType => "Strut";

            protected override float CasterOverride => ((Points[1].Z - Points[0].Z) / (Points[1].Y - Points[0].Y)).Atan() * 57.2957795f;

            protected override Tuple<Vector3, Vector3> WheelSteerAxisOverride => new Tuple<Vector3, Vector3>(Points[1], Points[0]);

            protected override IEnumerable<DebugLine> DebugLinesOverride => new [] {
                new DebugLine(Color.Red, Points[0], Points[1]),
                new DebugLine(Color.Yellow, Points[2], Points[5]),
                new DebugLine(Color.Yellow, Points[3], Points[5]),
                new DebugLine(Color.Cyan, Points[6], Points[7]),
                new DebugLine(Color.Gray, Points[5], Points[4]),
            };
        }
        #endregion

        #region Cameras
        public class CameraDescription {
            public CameraDescription(float fov, Vector3 position, float pitchAngle) {
                Fov = fov;
                Position = position;
                Look = Vector3.TransformNormal(Vector3.UnitZ, Matrix.RotationX(pitchAngle.ToRadians()));
                Up = Vector3.UnitY;
            }

            public CameraDescription(float fov, Vector3 position, Vector3 look, Vector3 up) {
                Fov = fov;
                Position = position;
                Look = look;
                Up = up;
            }

            public CameraDescription(float fov, Vector3 position, Vector3 look) : this(fov, position, look, Vector3.UnitY) { }

            public Vector3 Position { get; }

            public Vector3 Look { get; }

            public Vector3 Up { get; }

            public float Fov { get; }

            public FpsCamera ToCamera() {
                var camera = new FpsCamera(Fov.ToRadians()) { Position = Position };
                camera.LookAt(Position, Position + Look, Up);
                return camera;
            }
        }

        [CanBeNull]
        public CameraDescription GetDriverCamera() {
            if (_data.IsEmpty) return null;

            var section = _data.GetIniFile("car.ini")["GRAPHICS"];
            return new CameraDescription(56f,
                    section.GetSlimVector3("DRIVEREYES"),
                    section.GetFloat("ON_BOARD_PITCH_ANGLE", 0f));
        }

        [CanBeNull]
        public CameraDescription GetDashboardCamera() {
            if (_data.IsEmpty) return null;

            var section = _data.GetIniFile("dash_cam.ini")["DASH_CAM"];
            return new CameraDescription(56f,
                    section.GetSlimVector3("POS"),
                    section.GetFloat("PITCH_ANGLE", 0f));
        }

        [CanBeNull]
        public CameraDescription GetBonnetCamera() {
            if (_data.IsEmpty) return null;

            var section = _data.GetIniFile("car.ini")["GRAPHICS"];
            return new CameraDescription(56f,
                    section.GetSlimVector3("BONNET_CAMERA_POS") - GetGraphicOffset(),
                    section.GetFloat("BONNET_CAMERA_PITCH", 0f));
        }

        [CanBeNull]
        public CameraDescription GetBumperCamera() {
            if (_data.IsEmpty) return null;

            var section = _data.GetIniFile("car.ini")["GRAPHICS"];
            return new CameraDescription(56f,
                    section.GetSlimVector3("BUMPER_CAMERA_POS") - GetGraphicOffset(),
                    section.GetFloat("BUMPER_CAMERA_PITCH", 0f));
        }

        public IEnumerable<CameraDescription> GetExtraCameras() {
            return IsEmpty ? new CameraDescription[0] :
                    _data.GetIniFile("cameras.ini")
                         .GetSections("CAMERA")
                         .Select(x => new CameraDescription(x.GetFloat("FOV", 56f), x.GetSlimVector3("POSITION"), x.GetSlimVector3("FORWARD"), x.GetSlimVector3("UP")));
        }
        #endregion

        #region Wings animations
        public class WingAnimation : AnimationBase {
            public int? Next { get; }

            public WingAnimation(string ksAnimName, float duration, int? next) : base(ksAnimName, duration) {
                Next = next;
            }
        }

        public IEnumerable<WingAnimation> GetWingsAnimations() {
            return IsEmpty ? new WingAnimation[0] :
                    _data.GetIniFile("wing_animations.ini")
                         .GetSections("ANIMATION")
                         .Select(x => new WingAnimation(x.GetNonEmpty("FILE"), x.GetFloat("TIME", 1f), x.GetIntNullable("NEXT")))
                         .Where(x => x.KsAnimName != null)
                         .Distinct<WingAnimation>(AnimationBase.KsAnimNameComparer);
        }
        #endregion

        #region Extra animations
        public class ExtraAnimation : AnimationBase {
            public ExtraAnimation(string ksAnimName, float duration) : base(ksAnimName, duration) {}
        }

        public IEnumerable<ExtraAnimation> GetExtraAnimations() {
            return IsEmpty ? new ExtraAnimation[0] :
                    _data.GetIniFile("extra_animations.ini")
                         .GetSections("ANIMATION")
                         .Select(x => new ExtraAnimation(x.GetNonEmpty("FILE"), x.GetFloat("TIME", 1f)))
                         .Where(x => x.KsAnimName != null)
                         .Distinct<ExtraAnimation>(AnimationBase.KsAnimNameComparer);
        }

        public class RotatingObject {
            public RotatingObject(string nodeName, float rpm, Vector3 axis) {
                NodeName = nodeName;
                Rpm = rpm;
                Axis = axis;
            }

            public string NodeName { get; }

            public float Rpm { get; }

            public Vector3 Axis { get; }
        }

        public IEnumerable<RotatingObject> GetRotatingObjects() {
            return IsEmpty ? new RotatingObject[0] :
                    _data.GetIniFile("extra_animations.ini")
                         .GetSections("ROTATING_OBJECT")
                         .Select(x => new RotatingObject(x.GetNonEmpty("NAME"), x.GetFloat("RPM", 100f), x.GetSlimVector3("AXIS", Vector3.UnitY)))
                         .Where(x => x.NodeName != null);
        }
        #endregion

        #region Driver
        public class DriverDescription {
            public DriverDescription(string name, Vector3 offset, string steerAnimation, float steerAnimationLock) {
                Name = name;
                Offset = offset;
                SteerAnimation = steerAnimation;
                SteerAnimationLock = steerAnimationLock;
            }

            public string Name { get; }

            public Vector3 Offset { get; }

            public string SteerAnimation { get; }

            public float SteerAnimationLock { get; }
        }

        [CanBeNull]
        public DriverDescription GetDriverDescription() {
            if (IsEmpty) return null;

            var driver = _data.GetIniFile("driver3d.ini");

            var model = driver["MODEL"];
            var modelName = model.GetNonEmpty("NAME");
            if (modelName == null) return null;

            var steer = driver["STEER_ANIMATION"];
            return new DriverDescription(modelName, model.GetSlimVector3("POSITION"),
                    steer.GetNonEmpty("NAME", "steer.ksanim"), steer.GetFloat("LOCK", 360f));
        }
        #endregion

        #region Fuel
        public Vector3 GetFuelTankPosition() {
            return _data.GetIniFile("car.ini")["FUELTANK"].GetSlimVector3("POSITION");
        }

        /// <summary>
        /// In m³.
        /// </summary>
        public float GetFuelTankVolume() {
            return _data.GetIniFile("car.ini")["FUEL"].GetFloat("MAX_FUEL", 40f) * 0.001f;
        }
        #endregion
    }
}
