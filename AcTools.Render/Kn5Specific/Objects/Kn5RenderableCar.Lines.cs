// #define BB_PERF_PROFILE

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
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
        private class CarDebugLinesObject : IWithId<int> {
            public readonly int Id;
            public readonly string Name;
            public readonly IRenderableObject Renderable;
            public readonly Matrix Transform;
            public readonly Color4 Color;

            public CarDebugLinesObject(string name, IRenderableObject renderable, Color4? color = null) : this(-1, name, renderable, color) {}

            public CarDebugLinesObject(int id, string name, IRenderableObject renderable, Color4? color = null) {
                Id = id;
                Name = name;
                Renderable = renderable;
                Color = color ?? (renderable as DebugLinesObject)?.Color ??
                        (renderable as RenderableList)?.OfType<DebugLinesObject>().FirstOrDefault()?.Color ??
                                new Color4(1f, 0.6f, 0.3f, 0f);
                Transform = (renderable as DebugLinesObject)?.Transform ?? (renderable as RenderableList)?.LocalMatrix ?? Matrix.Identity;
            }

            [CanBeNull]
            public object Source { get; set; }

            public void SetMatrix(Matrix matrix) {
                var o = Renderable as DebugLinesObject;
                if (o != null) {
                    o.Transform = matrix;
                } else if (Renderable is RenderableList) {
                    ((RenderableList)Renderable).LocalMatrix = matrix;
                }
            }

            int IWithId<int>.Id => Id;
        }

        private class CarDebugLinesWrapper : IDisposable {
            [NotNull]
            private readonly Func<Kn5RenderableCar, CarData, IEnumerable<CarDebugLinesObject>> _lazy;

            [CanBeNull]
            private CarDebugLinesObject[] _lines;

            public CarDebugLinesWrapper([NotNull] Func<Kn5RenderableCar, CarData, CarDebugLinesObject> lazy) {
                _lazy = (c, d) => new [] { lazy(c, d) };
            }

            public CarDebugLinesWrapper([NotNull] Func<Kn5RenderableCar, CarData, IEnumerable<CarDebugLinesObject>> lazy) {
                _lazy = lazy;
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
                    if (string.IsNullOrWhiteSpace(line.Name)) return;

                    parent.DrawText(line.Name, line.Renderable.ParentMatrix * line.Transform, camera, screenSize, line.Color);
                }
            }

            public void Reset() {
                _lines?.Select(x => x.Renderable).DisposeEverything();
                _lines = null;
            }

            public void Dispose() {
                Reset();
            }
        }

        #region Suspension debug
        private bool _suspensionDebug;

        public bool SuspensionDebug {
            get { return _suspensionDebug; }
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
            for (var i = 0; i < suspension.DebugLines.Length; i++) {
                var line = suspension.DebugLines[i];
                result[index++] = new InputLayouts.VerticePC(pack.TranslateRelativeToCarModel(suspension, line.Start), line.Color.ToVector4());
                result[index++] = new InputLayouts.VerticePC(pack.TranslateRelativeToCarModel(suspension, line.End), line.Color.ToVector4());
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
            CarData.IndependentSuspensionsGroup ifg, irg;
            CarData.DependentSuspensionGroup dfg, drg;

            var index = 0;
            var result = new InputLayouts.VerticePC[(CountDebugSuspensionPoints(pack.Front, out ifg, out dfg) +
                    CountDebugSuspensionPoints(pack.Rear, out irg, out drg)) * 2];
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

        public bool DebugMode {
            get { return _currentLodObject.DebugMode; }
            set {
                if (Equals(value, DebugMode)) return;
                _currentLodObject.DebugMode = value;
                _skinsWatcherHolder?.RaiseSceneUpdated();
                OnPropertyChanged();

                if (_driver != null) {
                    _driver.DebugMode = value;
                }

                UpdateCrewDebugMode();
            }
        }

        [NotNull]
        public string CarId => Path.GetFileName(_rootDirectory) ?? "-";
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
            get { return _areWheelsContoursVisible; }
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
                        Vector3.UnitX, new Color4(1f, 0f, 1f, 0f), size: x.Radius);
                var b = DebugLinesObject.GetLinesCircle(Matrix.Translation(Vector3.UnitX * x.Width / 2f),
                        Vector3.UnitX, new Color4(1f, 0f, 1f, 0f), size: x.Radius);
                var r = DebugLinesObject.GetLinesCircle(Matrix.Translation((x.IsLeft ? 0.5f : -0.5f) * Vector3.UnitX * x.Width),
                        Vector3.UnitX, new Color4(1f, 0f, 1f, 0f), size: x.RimRadius);
                return new CarDebugLinesObject(x.Name, new RenderableList(x.Name, Matrix.Translation(x.Center) * graphicMatrix) { a, b, r });
            });
        });
        #endregion

        #region Fuel tank position
        private bool _isFuelTankVisible;

        public bool IsFuelTankVisible {
            get { return _isFuelTankVisible; }
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
                            proportions * side, new Color4(1f, 0.5f, 1f, 0f)));
        });
        #endregion

        #region Flames position
        private bool _areFlamesVisible;

        public bool AreFlamesVisible {
            get { return _areFlamesVisible; }
            set {
                if (Equals(value, _areFlamesVisible)) return;
                _areFlamesVisible = value;
                _skinsWatcherHolder?.RaiseSceneUpdated();
                _flamesLines.Reset();
                OnPropertyChanged();
            }
        }

        private readonly CarDebugLinesWrapper _flamesLines = new CarDebugLinesWrapper((car, data) => {
            return data.GetFlames().Select(x => new CarDebugLinesObject(x.Name,
                        DebugLinesObject.GetLinesArrow(Matrix.Translation(x.Position), x.Direction, new Color4(1f, 1f, 0f, 0f)))).ToArray();
        });
        #endregion

        #region Wings
        private bool _areWingsVisible;

        public bool AreWingsVisible {
            get { return _areWingsVisible; }
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
            return data.GetWings().Select((x, i) => new CarDebugLinesObject(i, x.Name,
                    DebugLinesObject.GetLinesPlane(
                            Matrix.RotationX(x.Angle.ToRadians()) * Matrix.Translation(x.Position) * graphicMatrix,
                            Vector3.UnitY, new Color4(1f, 0f, 1f, 1f), x.Span, x.Chord)) {
                                Source = x
                            }).ToArray();
        });

        private void UpdateWingLineAngle(int wing, float angle) {
            var x = _wingsLines.GetSource(wing) as CarData.WingDescription;
            if (x == null) return;

            var graphicMatrix = Matrix.Invert(_carData.GetGraphicMatrix());
            _wingsLines.SetMatrix(wing, Matrix.RotationX(angle) * Matrix.Translation(x.Position) * graphicMatrix);
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

            if (AreFlamesVisible) {
                _flamesLines.DrawLines(this, holder, camera);
            }

            if (AreWingsVisible) {
                _wingsLines.DrawLines(this, holder, camera);
            }
        }
        
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