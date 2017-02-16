using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using AcTools.DataFile;
using AcTools.Render.Base.Utils;
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

        public CarData([NotNull] string carDirectory) {
            _data = DataWrapper.FromDirectory(carDirectory);
        }

        public CarData([NotNull] DataWrapper data) {
            _data = data;
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

        #region Mirrors
        public IEnumerable<string> GetMirrorsNames() {
            return IsEmpty ? new string[0] :
                    _data.GetIniFile("mirrors.ini").GetSections("MIRROR").Select(section => section.GetNonEmpty("NAME")).Where(x => x != null);
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

        public class LightAnimation {
            public string KsAnimName { get; }

            public float Duration { get; }

            public LightAnimation(string ksAnimName, float duration) {
                KsAnimName = ksAnimName;
                Duration = duration;
            }

            private sealed class KsAnimNameEqualityComparer : IEqualityComparer<LightAnimation> {
                public bool Equals(LightAnimation x, LightAnimation y) {
                    if (ReferenceEquals(x, y)) return true;
                    if (ReferenceEquals(x, null)) return false;
                    if (ReferenceEquals(y, null)) return false;
                    if (x.GetType() != y.GetType()) return false;
                    return string.Equals(x.KsAnimName, y.KsAnimName);
                }

                public int GetHashCode(LightAnimation obj) {
                    return obj.KsAnimName?.GetHashCode() ?? 0;
                }
            }

            public static IEqualityComparer<LightAnimation> KsAnimNameComparer { get; } = new KsAnimNameEqualityComparer();
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

        public IEnumerable<LightAnimation> GetLightsAnimations() {
            return IsEmpty ? new LightAnimation[0] :
                    _data.GetIniFile("lights.ini")
                         .GetSections("LIGHT_ANIMATION")
                         .Select(x => new LightAnimation(x.GetNonEmpty("FILE"), x.GetFloat("TIME", 1f)))
                         .Where(x => x.KsAnimName != null);
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
                In = (float)fileSection.GetDouble("IN", 0d);
                Out = (float)fileSection.GetDouble("OUT", 0d);
            }
        }

        public IEnumerable<LodDescription> GetLods() {
            return IsEmpty ? new LodDescription[0] :
                    _data.GetIniFile("lods.ini").GetSections("LOD").Select(x => new LodDescription(x)).Where(x => x.FileName != null);
        }
        #endregion

        #region Steer lock
        public float GetSteerLock() {
            return _data.GetIniFile("car.ini")["CONTROLS"].GetFloat("STEER_LOCK", 180f);
        }
        #endregion

        #region Suspension
        public SuspensionsPack GetSuspensionsPack() {
            return SuspensionsPack.Create(_data);
        }

        public class SuspensionsPack {
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

            /*#region Debug lines
            public IReadOnlyList<DebugLine> DebugLines => _debugLines ?? (_debugLines = new [] {
                Front, Rear
            }.NonNull().SelectMany(x => x.DebugLines).ToArray());
            private DebugLine[] _debugLines;
            #endregion*/
        }

        public abstract class SuspensionsGroupBase {
            public static SuspensionsGroupBase Create(IniFile ini, bool front, float wheelRadius) {
                var basic = ini["BASIC"];
                var section = ini[front ? "FRONT" : "REAR"];
                var type = section.GetNonEmpty("TYPE");
                switch (type) {
                    case "DWB":
                        return new IndependentSuspensionsGroup(
                                new DwbSuspension(basic, section, front, -1f, wheelRadius),
                                new DwbSuspension(basic, section, front, 1f, wheelRadius));

                    case "STRUT":
                        return new IndependentSuspensionsGroup(
                                new StrutSuspension(basic, section, front, -1f, wheelRadius),
                                new StrutSuspension(basic, section, front, 1f, wheelRadius));

                    case "AXLE":
                        return new DependentSuspensionGroup(
                                new AxleSuspension(basic, section, ini["AXLE"], front, wheelRadius));

                    default:
                        AcToolsLogging.Write($"Unknown suspension type: “{type}”");
                        return null;
                }
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
        }

        public class DependentSuspensionGroup : SuspensionsGroupBase {
            public DependentSuspensionGroup(SuspensionBase both) {
                Both = both;
            }

            [NotNull]
            public SuspensionBase Both { get; }
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

            protected override float KpiOverride => MathF.Atan((this.Points[4].X - this.Points[5].X) / (this.Points[4].Y - this.Points[5].Y)) * 57.2957795f;

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

            protected override float CasterOverride => MathF.Atan((Points[4].Z - Points[5].Z) / (Points[4].Y - Points[5].Y)) * 57.2957795f;

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

            protected override float CasterOverride => MathF.Atan((Points[1].Z - Points[0].Z) / (Points[1].Y - Points[0].Y)) * 57.2957795f;

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
    }
}
