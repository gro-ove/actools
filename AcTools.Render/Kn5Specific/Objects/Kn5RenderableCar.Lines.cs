// #define BB_PERF_PROFILE

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using AcTools.DataFile;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Sprites;
using AcTools.Render.Base.Structs;
using AcTools.Render.Base.Utils;
using AcTools.Render.Data;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;
using SlimDX.DirectWrite;
using FontStyle = SlimDX.DirectWrite.FontStyle;
using TextAlignment = AcTools.Render.Base.Sprites.TextAlignment;

namespace AcTools.Render.Kn5Specific.Objects {
    public partial class Kn5RenderableCar {
        private class CarDebugLinesObject : IWithId<int>, IMoveable {
            private readonly int _id;
            public readonly string Name;
            public string OriginalName;
            public readonly IRenderableObject Renderable;
            public Matrix Transform;
            public readonly Color4 Color;

            public MoveableRotationAxis AllowRotation { get; set; }

            public bool AllowScaling { get; set; }

            public CarDebugLinesObject(string name, IRenderableObject renderable, Color4? color = null)
                    : this(-1, name, renderable, color) { }

            public CarDebugLinesObject(int id, string name, IRenderableObject renderable, Color4? color = null) {
                _id = id;
                Name = name;
                OriginalName = name;
                Renderable = renderable;
                Color = color ?? (renderable as DebugLinesObject)?.Color ??
                        (renderable as RenderableList)?.OfType<DebugLinesObject>().FirstOrDefault()?.Color ??
                                new Color4(1f, 0.6f, 0.3f, 0f);
                Transform = (renderable as DebugLinesObject)?.Transform ?? (renderable as RenderableList)?.LocalMatrix ?? Matrix.Identity;
            }

            [CanBeNull]
            public object Source { get; set; }

            public void SetMatrix(Matrix matrix) {
                if (Renderable is DebugLinesObject o) {
                    o.Transform = matrix;
                } else if (Renderable is RenderableList list) {
                    list.LocalMatrix = matrix;
                }
            }

            int IWithId<int>.Id => _id;

            [CanBeNull]
            public Func<CarDebugLinesObject, CarDebugLinesObject> CloneFunc { get; set; }

            private MoveableHelper _movable;
            public MoveableHelper Movable => _movable ?? (_movable = new MoveableHelper(this, AllowRotation, AllowScaling));

            void IMoveable.Move(Vector3 delta) {
                Transform *= Matrix.Translation(delta);
                SetMatrix(Transform);
            }

            void IMoveable.Rotate(Quaternion delta) {
                Transform = Matrix.RotationQuaternion(delta) * Transform;
                SetMatrix(Transform);
            }

            public void Scale(Vector3 scale) {
                Transform *= Matrix.Scaling(scale);
                SetMatrix(Transform);
            }

            public IMoveable Clone() {
                return CloneFunc?.Invoke(this);
            }
        }

        private class CarDebugLinesWrapper : IDisposable {
            [NotNull]
            private readonly Func<Kn5RenderableCar, CarData, IEnumerable<CarDebugLinesObject>> _lazy;

            [CanBeNull]
            private readonly Func<Kn5RenderableCar, CarData, CarDebugLinesObject, CarDebugLinesWrapper, CarDebugLinesObject> _cloneFunc;

            [CanBeNull]
            private CarDebugLinesObject[] _lines;

            public CarDebugLinesWrapper([NotNull] Func<Kn5RenderableCar, CarData, CarDebugLinesObject> lazy) {
                _lazy = (c, d) => new[] { lazy(c, d) };
            }

            public CarDebugLinesWrapper([NotNull] Func<Kn5RenderableCar, CarData, IEnumerable<CarDebugLinesObject>> lazy,
                    Func<Kn5RenderableCar, CarData, CarDebugLinesObject, CarDebugLinesWrapper, CarDebugLinesObject> cloneFunc = null) {
                _lazy = lazy;
                _cloneFunc = cloneFunc;
            }

            private void Initialize([NotNull] Kn5RenderableCar parent) {
                if (_lines == null) {
                    _lines = _lazy.Invoke(parent, parent._carData)?.ToArray() ?? new CarDebugLinesObject[0];
                }
            }

            [CanBeNull]
            public object GetSource(int id) {
                return _lines?.GetByIdOrDefault(id)?.Source;
            }

            public void SetMatrix(int id, Matrix matrix) {
                _lines?.GetByIdOrDefault(id)?.SetMatrix(matrix);
            }

            public void DrawLines([NotNull] Kn5RenderableCar parent, IDeviceContextHolder holder, ICamera camera) {
                Initialize(parent);

                var lines = _lines;
                if (lines == null) return;

                for (var i = lines.Length - 1; i >= 0; i--) {
                    var line = lines[i];
                    line.Renderable.ParentMatrix = parent.RootObject.Matrix;
                    line.Renderable.Draw(holder, camera, SpecialRenderMode.Simple);
                }
            }

            public void DrawLabels([NotNull] Kn5RenderableCar parent, ICamera camera, Vector2 screenSize) {
                Initialize(parent);

                var lines = _lines;
                if (lines == null) return;

                for (var i = lines.Length - 1; i >= 0; i--) {
                    var line = lines[i];
                    parent.DrawText(line.Name, line.Transform * parent.Matrix, camera, screenSize, line.Color);
                }
            }

            [CanBeNull]
            private Kn5RenderableCar _cloneParent;

            [CanBeNull]
            private CarDebugLinesObject ItemCloneFunc(CarDebugLinesObject clonee) {
                if (_cloneParent == null) return null;
                return _cloneFunc?.Invoke(_cloneParent, _cloneParent._carData, clonee, this);
            }

            public void DrawMovementArrows([NotNull] Kn5RenderableCar parent, DeviceContextHolder holder, CameraBase camera) {
                var lines = _lines;
                if (lines == null) return;

                _cloneParent = parent;
                for (var i = lines.Length - 1; i >= 0; i--) {
                    var line = lines[i];
                    line.CloneFunc = ItemCloneFunc;
                    line.Movable.ParentMatrix = line.Transform * parent.Matrix;
                    line.Movable.Draw(holder, camera, SpecialRenderMode.Simple);
                }
            }

            public bool MoveObject(Vector2 relativeFrom, Vector2 relativeDelta, CameraBase camera, bool tryToClone, out IMoveable cloned) {
                var lines = _lines;
                if (lines != null) {
                    for (var i = lines.Length - 1; i >= 0; i--) {
                        var m = lines[i];
                        if (m.Movable.MoveObject(relativeFrom, relativeDelta, camera, tryToClone, out cloned)) {
                            if (cloned is CarDebugLinesObject o) {
                                _lines = _lines?.Append(o).ToArray();
                            }
                            return true;
                        }
                    }
                }
                cloned = null;
                return false;
            }

            public void StopMovement() {
                var lines = _lines;
                if (lines == null) return;
                for (var i = lines.Length - 1; i >= 0; i--) {
                    lines[i].Movable.StopMovement();
                }
            }

            public void Reset() {
                _lines?.Select(x => x.Renderable).DisposeEverything();
                _lines = null;
            }

            public void Dispose() {
                Reset();
            }

            public int Count([NotNull] Kn5RenderableCar parent) {
                Initialize(parent);
                return _lines?.Length ?? 0;
            }

            public class TransformEntry {
                public string Name;
                public string OriginalName;
                public Matrix Transform;
            }

            public IEnumerable<TransformEntry> GetTransforms() {
                return _lines?.Select(x => new TransformEntry {
                    Name = x.Name,
                    OriginalName = x.OriginalName,
                    Transform = x.Transform
                }) ?? new TransformEntry[0];
            }
        }

        #region Suspension debug
        private bool _suspensionDebug;

        public bool SuspensionDebug {
            get => _suspensionDebug;
            set {
                if (Equals(value, _suspensionDebug)) return;
                _suspensionDebug = value;
                _skinsWatcherHolder?.RaiseUpdateRequired();
                OnPropertyChanged();
            }
        }

        public CarData.SuspensionsPack SuspensionsPack => _suspensionsPack ?? (_suspensionsPack = _carData.GetSuspensionsPack());
        private CarData.SuspensionsPack _suspensionsPack;

        private IRenderableObject _suspensionLines;
        private DebugObject _debugNode;

        private static int CountDebugSuspensionPoints(CarData.SuspensionsGroupBase group,
                out CarData.IndependentSuspensionsGroup independent, out CarData.DependentSuspensionGroup dependent) {
            independent = group as CarData.IndependentSuspensionsGroup;
            if (independent != null) {
                dependent = null;
                return independent.Left.DebugLines.Length + independent.Right.DebugLines.Length;
            }

            dependent = group as CarData.DependentSuspensionGroup;
            return dependent?.Both.DebugLines.Length ?? 0;
        }

        private static void AddDebugSuspensionPoints(CarData.SuspensionsPack pack, CarData.SuspensionBase suspension, InputLayouts.VerticePC[] result,
                ref int index) {
            foreach (var line in suspension.DebugLines) {
                result[index++] = new InputLayouts.VerticePC(pack.TranslateRelativeToCarModel(suspension, line.Start), line.ColorStart.ToVector4());
                result[index++] = new InputLayouts.VerticePC(pack.TranslateRelativeToCarModel(suspension, line.End), line.ColorEnd.ToVector4());
            }
        }

        private static void AddDebugSuspensionPoints(CarData.SuspensionsPack pack, InputLayouts.VerticePC[] result,
                CarData.IndependentSuspensionsGroup independent, CarData.DependentSuspensionGroup dependent, ref int index) {
            if (independent != null) {
                AddDebugSuspensionPoints(pack, independent.Left, result, ref index);
                AddDebugSuspensionPoints(pack, independent.Right, result, ref index);
            } else if (dependent != null) {
                AddDebugSuspensionPoints(pack, dependent.Both, result, ref index);
            }
        }

        private static InputLayouts.VerticePC[] GetDebugSuspensionVertices(CarData.SuspensionsPack pack) {
            var index = 0;
            var result = new InputLayouts.VerticePC[(CountDebugSuspensionPoints(pack.Front, out var ifg, out var dfg) +
                    CountDebugSuspensionPoints(pack.Rear, out var irg, out var drg)) * 2];
            AddDebugSuspensionPoints(pack, result, ifg, dfg, ref index);
            AddDebugSuspensionPoints(pack, result, irg, drg, ref index);
            return result;
        }

        public void DrawSuspensionDebugStuff(DeviceContextHolder holder, ICamera camera) {
            if (_suspensionLines == null) {
                _suspensionLines = new DebugLinesObject(Matrix.Identity, GetDebugSuspensionVertices(SuspensionsPack));
            }

            _suspensionLines.ParentMatrix = RootObject.Matrix;
            _suspensionLines.Draw(holder, camera, SpecialRenderMode.Simple);

            if (_wheelLfCon != default(Vector3)) {
                if (_debugNode == null) {
                    _debugNode = new DebugObject(Matrix.Translation(_wheelLfCon), GeometryGenerator.CreateSphere(0.02f, 6, 6));
                }

                _debugNode.Transform = Matrix.Translation(_wheelLfCon);
                _debugNode.ParentMatrix = RootObject.Matrix;

                holder.DeviceContext.OutputMerger.DepthStencilState = holder.States.DisabledDepthState;
                _debugNode.Draw(holder, camera, SpecialRenderMode.Simple);
            }
        }

        [CanBeNull]
        private List<SuspensionMovablePoint> _suspensionMovables;

        public abstract class SuspensionMovablePoint : IMoveable, IDisposable {
            [NotNull]
            public MoveableHelper Movable;

            [NotNull]
            private readonly CarData.SuspensionBase _target;

            public bool Front;

            protected SuspensionMovablePoint([NotNull] CarData.SuspensionBase target, bool front) {
                Movable = new MoveableHelper(this, MoveableRotationAxis.None);
                _target = target;
                Front = front;
            }

            protected abstract Vector3 GetPosition();
            protected abstract void ApplyMovement(Vector3 delta);

            public void Draw(DeviceContextHolder holder, ICamera camera, CarData.SuspensionsPack pack, Matrix parent) {
                Movable.ParentMatrix = Matrix.Translation(GetPosition()) * pack.TranslateRelativeToCarModel(_target) * parent;
                Movable.Draw(holder, camera, SpecialRenderMode.Simple);
            }

            public void Save(IniFile suspensions) {
                _target.SavePoints(suspensions, Front);
            }

            void IMoveable.Move(Vector3 delta) {
                _target.ResetLinesCache();
                ApplyMovement(delta);
            }

            void IMoveable.Rotate(Quaternion delta) { }

            void IMoveable.Scale(Vector3 scale) { }

            IMoveable IMoveable.Clone() {
                return null;
            }

            public void Dispose() {
                Movable.Dispose();
            }
        }

        public class AxleSuspensionMovablePoint : SuspensionMovablePoint {
            private readonly CarData.AxleLink _link;
            private readonly bool _carSide;

            public AxleSuspensionMovablePoint([NotNull] CarData.SuspensionBase target, CarData.AxleLink link, bool carSide, bool front)
                    : base(target, front) {
                _link = link;
                _carSide = carSide;
            }

            protected override Vector3 GetPosition() {
                return _carSide ? _link.Car : _link.Axle;
            }

            protected override void ApplyMovement(Vector3 delta) {
                if (_carSide) {
                    _link.Car += delta;
                } else {
                    _link.Axle += delta;
                }
            }
        }

        public class EightPointsSuspensionMovablePoint : SuspensionMovablePoint {
            [NotNull]
            private CarData.EightPointsSuspensionBase _target;

            [CanBeNull]
            private CarData.EightPointsSuspensionBase _targetAlt;

            private int _pointIndex;

            public EightPointsSuspensionMovablePoint([NotNull] CarData.EightPointsSuspensionBase target, int index, bool front,
                    [CanBeNull] CarData.EightPointsSuspensionBase targetAlt) : base(target, front) {
                _target = target;
                _pointIndex = index;
                _targetAlt = targetAlt;
            }

            protected override Vector3 GetPosition() {
                return _target.Points[_pointIndex];
            }

            protected override void ApplyMovement(Vector3 delta) {
                _target.Points[_pointIndex] += delta;
                if (_targetAlt != null) {
                    _targetAlt.ResetLinesCache();
                    delta.X *= _targetAlt.XOffset * _target.XOffset;
                    _targetAlt.Points[_pointIndex] += delta;
                }
            }
        }

        public void DrawSuspensionMovementArrows(DeviceContextHolder holder, ICamera camera) {
            var suspension = _suspensionMovables;
            if (suspension == null) {
                suspension = new List<SuspensionMovablePoint>();
                InitMovableGroup(SuspensionsPack.Front, true);
                InitMovableGroup(SuspensionsPack.Rear, false);
                _suspensionMovables = suspension;
            }

            void InitMovable(bool front, CarData.SuspensionBase suspensionBase, CarData.SuspensionBase otherSide = null) {
                if (suspensionBase is CarData.EightPointsSuspensionBase eightPoint) {
                    var other = otherSide as CarData.EightPointsSuspensionBase;
                    for (var i = 0; i < 8; i++) {
                        suspension.Add(new EightPointsSuspensionMovablePoint(eightPoint, i, front, other));
                    }
                } else if (suspensionBase is CarData.AxleSuspension axle) {
                    foreach (var t in axle.Links) {
                        suspension.Add(new AxleSuspensionMovablePoint(axle, t, true, front));
                        suspension.Add(new AxleSuspensionMovablePoint(axle, t, false, front));
                    }
                }
            }

            void InitMovableGroup(CarData.SuspensionsGroupBase group, bool front) {
                if (group is CarData.IndependentSuspensionsGroup gi) {
                    InitMovable(front, gi.Left, gi.Right);
                }
                if (group is CarData.DependentSuspensionGroup gd) {
                    InitMovable(front, gd.Both);
                }
            }

            foreach (var m in suspension) {
                m.Draw(holder, camera, SuspensionsPack, RootObject.Matrix);
            }
        }

        public bool MoveSuspension(Vector2 relativeFrom, Vector2 relativeDelta, CameraBase camera) {
            var suspension = _suspensionMovables;
            if (suspension != null) {
                foreach (var m in suspension) {
                    if (m.Movable.MoveObject(relativeFrom, relativeDelta, camera, false, out _)) {
                        SuspensionsPack.RaiseMeasurementsChanged();
                        _suspensionLines?.Dispose();
                        _suspensionLines = null;
                        return true;
                    }
                }
            }
            return false;
        }

        public void StopSuspensionMovement() {
            var suspension = _suspensionMovables;
            if (suspension != null) {
                foreach (var m in suspension) {
                    m.Movable.StopMovement();
                }
            }
        }

        [NotNull]
        public string CarId => Path.GetFileName(RootDirectory);
        #endregion

        #region Colliders from colliders.ini
        private readonly CarDebugLinesWrapper _colliderLines = new CarDebugLinesWrapper((car, data) => {
            var graphicMatrix = Matrix.Invert(data.GetGraphicMatrix());
            return data.GetColliders().Select(x => new CarDebugLinesObject(x.Name, DebugLinesObject.GetLinesBox(
                    Matrix.Translation(x.Center) * graphicMatrix,
                    x.Size, new Color4(1f, 1f, 0f, 0f))));
        });
        #endregion

        #region Wheels contours
        private bool _areWheelsContoursVisible;

        public bool AreWheelsContoursVisible {
            get => _areWheelsContoursVisible;
            set {
                if (Equals(value, _areWheelsContoursVisible)) return;
                _areWheelsContoursVisible = value;
                _skinsWatcherHolder?.RaiseSceneUpdated();
                _wheelsLines.Reset();
                OnPropertyChanged();
            }
        }

        private readonly CarDebugLinesWrapper _wheelsLines = new CarDebugLinesWrapper((car, data) => {
            var graphicMatrix = Matrix.Invert(data.GetGraphicMatrix());
            return data.GetWheels().Select(x => {
                var a = DebugLinesObject.GetLinesCircle(Matrix.Translation(-Vector3.UnitX * x.Width / 2f),
                        Vector3.UnitX, new Color4(1f, 0f, 1f, 0f), radius: x.Radius);
                var b = DebugLinesObject.GetLinesCircle(Matrix.Translation(Vector3.UnitX * x.Width / 2f),
                        Vector3.UnitX, new Color4(1f, 0f, 1f, 0f), radius: x.Radius);
                var r = DebugLinesObject.GetLinesCircle(Matrix.Translation((x.IsLeft ? 0.5f : -0.5f) * Vector3.UnitX * x.Width),
                        Vector3.UnitX, new Color4(1f, 0f, 1f, 0f), radius: x.RimRadius);

                var matrix = Matrix.Translation(x.CenterWheel);

                if (x.StaticToe != 0f) {
                    matrix = Matrix.RotationY(-x.CenterWheel.X.Sign() * x.StaticToe.ToRadians()) * matrix;
                }

                if (x.StaticCamber != 0f) {
                    matrix = Matrix.RotationZ(-x.CenterWheel.X.Sign() * x.StaticCamber.ToRadians()) * matrix;
                }

                return new CarDebugLinesObject(x.Name, new RenderableList(x.Name, matrix * graphicMatrix) { a, b, r });
            });
        });
        #endregion

        #region Fuel tank position
        private bool _isFuelTankVisible;

        public bool IsFuelTankVisible {
            get => _isFuelTankVisible;
            set {
                if (Equals(value, _isFuelTankVisible)) return;
                _isFuelTankVisible = value;
                _skinsWatcherHolder?.RaiseSceneUpdated();
                _fuelTankLines.Reset();
                OnPropertyChanged();
            }
        }

        private readonly CarDebugLinesWrapper _fuelTankLines = new CarDebugLinesWrapper((car, data) => {
            var volume = data.GetFuelTankVolume();
            var side = volume.Pow(1f / 3f);
            var proportions = new Vector3(2f, 0.5f, 1f);
            return new CarDebugLinesObject("Fuel tank",
                    DebugLinesObject.GetLinesBox(Matrix.Translation(data.GetFuelTankPosition()) * Matrix.Invert(data.GetGraphicMatrix()),
                            proportions * side, new Color4(1f, 0.5f, 1f, 0f))) {
                                AllowScaling = false
                            };
        });
        #endregion

        #region Inertia box
        private bool _isInertiaBoxVisible;

        public bool IsInertiaBoxVisible {
            get => _isInertiaBoxVisible;
            set {
                if (Equals(value, _isInertiaBoxVisible)) return;
                _isInertiaBoxVisible = value;
                _skinsWatcherHolder?.RaiseSceneUpdated();
                _inertiaBoxLines.Reset();
                OnPropertyChanged();
            }
        }

        private readonly CarDebugLinesWrapper _inertiaBoxLines = new CarDebugLinesWrapper((car, data) => {
            var size = data.GetInertiaBox();
            return new CarDebugLinesObject("Inertia box",
                    DebugLinesObject.GetLinesBox(Matrix.Translation(new Vector3(0f, size.Y / 2f, 0f)),
                            size, new Color4(1f, 0f, 0.7f, 1f))) { AllowScaling = false };
        });
        #endregion

        #region Flames position
        private bool _areFlamesVisible;

        public bool AreFlamesVisible {
            get => _areFlamesVisible;
            set {
                if (Equals(value, _areFlamesVisible)) return;
                _areFlamesVisible = value;
                _skinsWatcherHolder?.RaiseSceneUpdated();
                _flamesLines.Reset();
                OnPropertyChanged();
            }
        }

        private static Matrix GetFlameMatrix(CarData.FlameDescription x) {
            var side = Vector3.Cross(x.Direction, Math.Abs(x.Direction.Y) > 0.5 ? -Vector3.UnitZ : Vector3.UnitY);
            return Matrix.Invert(Matrix.LookAtRH(Vector3.Zero, -x.Direction, Vector3.Normalize(Vector3.Cross(x.Direction, side))))
                    * Matrix.Translation(x.Position);
        }

        private readonly CarDebugLinesWrapper _flamesLines = new CarDebugLinesWrapper((car, data) => {
            return data.GetFlames().Select(x => {
                var renderable = DebugLinesObject.GetLinesArrow(GetFlameMatrix(x), Vector3.UnitZ, new Color4(1f, 1f, 0f, 0f));
                return new CarDebugLinesObject(x.Name, renderable) {
                    AllowRotation = MoveableRotationAxis.All
                };
            }).ToArray();
        }, (car, data, original, parent) => {
            var renderable = DebugLinesObject.GetLinesArrow(original.Transform, Vector3.UnitZ,
                    new Color4(1f, 1f, 0f, 0f));
            return new CarDebugLinesObject("FLAME_" + parent.Count(car), renderable) {
                OriginalName = original.OriginalName,
                AllowRotation = MoveableRotationAxis.All
            };
        });
        #endregion

        #region Wings
        private bool _areWingsVisible;

        public bool AreWingsVisible {
            get => _areWingsVisible;
            set {
                if (Equals(value, _areWingsVisible)) return;
                _areWingsVisible = value;
                _skinsWatcherHolder?.RaiseSceneUpdated();
                _wingsLines.Reset();
                OnPropertyChanged();
            }
        }

        private readonly CarDebugLinesWrapper _wingsLines = new CarDebugLinesWrapper((car, data) => {
            var graphicMatrix = Matrix.Invert(data.GetGraphicMatrix());

            CarDebugLinesObject GetWingLines(CarData.WingDescription x, int i) {
                var plane = DebugLinesObject.GetLinesPlane(
                        Matrix.RotationX(x.Angle.ToRadians()) * Matrix.Translation(x.Position) * graphicMatrix, 
                        Vector3.UnitY, new Color4(1f, 0f, 1f, 1f), x.Span, x.Chord);
                return new CarDebugLinesObject(i, x.Name, plane) { Source = x, OriginalName = x.SectionName };
            }

            CarDebugLinesObject GetFinLines(CarData.FinDescription x, int i) {
                var plane = DebugLinesObject.GetLinesPlane(
                        Matrix.RotationZ(MathF.PI / 2f) * Matrix.RotationX(x.Angle.ToRadians()) * Matrix.Translation(x.Position) * graphicMatrix, 
                        Vector3.UnitY, new Color4(1f, 0f, 0f, 1f), x.Span, x.Chord);
                return new CarDebugLinesObject(i, x.Name, plane) { Source = x, OriginalName = x.SectionName };
            }

            return data.GetWings().Select(GetWingLines).Concat(data.GetFins().Select(GetFinLines)).ToArray();
        });

        private void UpdateWingLineAngle(int wing, float angle) {
            if (_wingsLines.GetSource(wing) is CarData.WingDescription x) {
                var graphicMatrix = Matrix.Invert(_carData.GetGraphicMatrix());
                _wingsLines.SetMatrix(wing, Matrix.RotationX(angle) * Matrix.Translation(x.Position) * graphicMatrix);
            }
        }
        #endregion

        #region Draw debug stuff
        public void DrawDebug(DeviceContextHolder holder, ICamera camera) {
            if (SuspensionDebug) {
                DrawSuspensionDebugStuff(holder, camera);
            }

            if (IsColliderVisible) {
                _colliderLines.DrawLines(this, holder, camera);
            }

            if (AreWheelsContoursVisible) {
                _wheelsLines.DrawLines(this, holder, camera);
            }

            if (IsFuelTankVisible) {
                _fuelTankLines.DrawLines(this, holder, camera);
            }

            if (IsInertiaBoxVisible) {
                _inertiaBoxLines.DrawLines(this, holder, camera);
            }

            if (AreFlamesVisible) {
                _flamesLines.DrawLines(this, holder, camera);
            }

            if (AreWingsVisible) {
                _wingsLines.DrawLines(this, holder, camera);
            }
        }

        private void DrawText(string text, Matrix objectTransform, ICamera camera, Vector2 screenSize, Color4 color) {
            if (string.IsNullOrWhiteSpace(text)) return;
            var onScreenPosition = Vector3.TransformCoordinate(Vector3.Zero, objectTransform * camera.ViewProj) * 0.5f +
                    new Vector3(0.5f);
            onScreenPosition.Y = 1f - onScreenPosition.Y;
            _debugText.DrawString(text,
                    new RectangleF(onScreenPosition.X * screenSize.X - 100f, onScreenPosition.Y * screenSize.Y - 70f, 200f, 200f), 0f,
                    TextAlignment.HorizontalCenter | TextAlignment.VerticalCenter, 12f, color,
                    CoordinateType.Absolute);
        }

        public void DrawSprites(SpriteRenderer sprite, ICamera camera, Vector2 screenSize) {
            if (_debugText == null) {
                _debugText = new TextBlockRenderer(sprite, "Arial", FontWeight.Normal, FontStyle.Normal, FontStretch.Normal, 16f);
            }

            if (IsColliderVisible) {
                _colliderLines.DrawLabels(this, camera, screenSize);
            }

            if (AreWheelsContoursVisible) {
                _wheelsLines.DrawLabels(this, camera, screenSize);
            }

            if (IsFuelTankVisible) {
                _fuelTankLines.DrawLabels(this, camera, screenSize);
            }

            if (AreFlamesVisible) {
                _flamesLines.DrawLabels(this, camera, screenSize);
            }

            if (AreWingsVisible) {
                _wingsLines.DrawLabels(this, camera, screenSize);
            }
        }

        private TextBlockRenderer _debugText;
        #endregion
    }
}