using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Data;
using AcTools.Render.Temporary;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;

namespace AcTools.Render.Kn5Specific.Objects {
    public partial class Kn5RenderableCar {
        private Matrix GetWheelAmbientShadowMatrix([NotNull] Kn5RenderableList wheel) {
            // try to get information about the wheel if available
            var desc = _wheelsDesc?.FirstOrDefault(x => wheel.Name?.EndsWith(x.Name) == true);

            // find relative-to-car wheel matrix
            Matrix wheelMatrix;
            if (desc == null) {
                UpdateModelMatrixInverted();
                wheelMatrix = wheel.Matrix * wheel.ModelMatrixInverted;
            } else {
                wheelMatrix = GetWheelMatrix(desc, true) * _wheelsFixMatrix;
            }

            // calculate shadow position
            var translation = wheelMatrix.GetTranslationVector();

            // offset if needed
            translation += _carData.GetWheelGraphicOffset(wheel.Name);

            // for properly set wheels, calculate offset
            if (desc != null) {
                var down = Vector3.TransformNormal(-Vector3.UnitY, wheelMatrix);
                translation.X += down.X * (wheel.BoundingBox?.GetSize().Y ?? 0f) / 2f;
            }

            // move the shadow down
            translation.Y = _shadowsHeight;

            return Matrix.Scaling(_carData.GetWheelShadowSize()) * // shadow size
                    Matrix.RotationY(MathF.PI - _steerDeg * MathF.PI / 180f) * // steering
                    Matrix.Translation(translation);
        }

        #region Steering
        private float _steerDeg;

        public float SteerDeg {
            get => _steerDeg;
            set {
                value = value.Clamp(-50f, 50f).Round(0.1f);
                if (Equals(value, _steerDeg)) return;
                _steerDeg = value;
                OnPropertyChanged();
                UpdatePreudoSteer();
            }
        }

        private Vector3 _wheelLfCon;
        private float _steerDegPrevious, _wheelsPosPrevious;

        private Matrix _wheelsFixMatrix;

        private Matrix? GetSteerWheelMatrix(bool left, [NotNull] Kn5RenderableList node, Tuple<Vector3, Vector3> axis, float angle) {
            Matrix baseMatrix;
            if (AlignWheelsByData) {
                var wheel = left ? _wheelLfDesc : _wheelRfDesc;
                if (wheel != null) {
                    var isWheel = IsWheelNode(node);
                    baseMatrix = GetWheelMatrix(wheel, !isWheel) * _wheelsFixMatrix;
                    if (isWheel) {
                        baseMatrix = GetWheelSpeedMatrix(_wheelsPosition, wheel.Radius) * baseMatrix;
                    }

                    goto BaseMatrixSet;
                }
            }

            baseMatrix = _currentLodObject.GetOriginalRelativeToModelMatrix(node);

            BaseMatrixSet:
            Vector3 position, scale;
            Quaternion rotation;
            baseMatrix.Decompose(out scale, out rotation, out position);

            var rotationAxis = Vector3.Normalize(axis.Item2 - axis.Item1);
            var p = new Plane(position, rotationAxis);
            Vector3 con;
            if (!Plane.Intersects(p, axis.Item1 - rotationAxis * 10f, axis.Item2 + rotationAxis * 10f, out con)) {
                AcToolsLogging.Write("10f is not enough!?");
                return null;
            }

#if DEBUG
            _wheelLfCon = con;
#endif

            var delta = con - position;
            var transform = Matrix.Translation(-delta) * Matrix.RotationAxis(rotationAxis, angle.ToRadians()) *
                    Matrix.Translation(delta);
            return Matrix.Scaling(scale) * Matrix.RotationQuaternion(rotation) * transform * Matrix.Translation(position);
        }

        [ItemNotNull]
        private static IEnumerable<string> GetWheelNodesNames([NotNull] IRenderableObject parent, string namePostfix) {
            var c = parent as Kn5RenderableCar;
            var getDummyByName = c != null ? (Func<string, RenderableList>)c.GetDummyByName :
                ((RenderableList)parent).GetDummyByName;

            var susp = $@"SUSP_{namePostfix}";
            var hub = $@"HUB_{namePostfix}";
            var skipHub = getDummyByName(susp).GetAllChildren().Any(x => x.Name == hub);

            var animatedDisc = $@"DISC_{namePostfix}_ANIM";
            return new[] {
                $@"WHEEL_{namePostfix}", susp, skipHub ? null : hub,
                getDummyByName(animatedDisc) != null ? null : $@"DISC_{namePostfix}"
            }.NonNull();
        }

        private delegate Matrix? GetWheelNodeMatrix(Kn5RenderableList node);

        [ContractAnnotation(@"collectNodes:true => notnull; collectNodes:false => null")]
        private static List<Kn5RenderableList> SetWheelNodeMatrix([NotNull] IRenderableObject parent, string namePostfix,
                [NotNull] GetWheelNodeMatrix callback, bool collectNodes) {
            var c = parent as Kn5RenderableCar;
            var getDummyByName = c != null ? (Func<string, RenderableList>)c.GetDummyByName :
                ((RenderableList)parent).GetDummyByName;

            var names = GetWheelNodesNames(parent, namePostfix);

            var wheel = getDummyByName($@"WHEEL_{namePostfix}") as Kn5RenderableList;
            var result = collectNodes ? new List<Kn5RenderableList>() : null;
            if (wheel == null) return result;

            var wheelMatrix = callback(wheel);
            if (!wheelMatrix.HasValue) return result;

            foreach (var node in
                    (c?.Dummies ?? ((RenderableList)parent).GetAllChildren().OfType<Kn5RenderableList>()).Where(x => names.NonNull().Contains(x.Name))) {
                /*AcToolsLogging.Write(node.Name);
                AcToolsLogging.Write(node.LocalMatrix);
                AcToolsLogging.Write(node.GetOriginalScale().ToString());
                AcToolsLogging.Write((node[0] as Kn5RenderableList)?.GetOriginalScale().ToString());

                AcToolsLogging.Write("original: " + node.GetOriginalScale());
                AcToolsLogging.Write("callback: " + callback(node));
                AcToolsLogging.Write("wm: " + wheelMatrix.Value);
                AcToolsLogging.Write("parent: " + node.ParentMatrix);
                AcToolsLogging.Write("mmi: " + node.ModelMatrixInverted);
                AcToolsLogging.Write("parent×mmi: " + node.ParentMatrix * node.ModelMatrixInverted);
                AcToolsLogging.Write("inv: " + Matrix.Invert(node.ParentMatrix * node.ModelMatrixInverted));
                AcToolsLogging.Write("res: " + (callback(node) ?? wheelMatrix.Value) *
                        Matrix.Invert(node.ParentMatrix * node.ModelMatrixInverted));*/

                node.LocalMatrix = /*Matrix.Scaling(node.GetOriginalScale()) **/ (callback(node) ?? wheelMatrix.Value) *
                        Matrix.Invert(node.ParentMatrix * node.ModelMatrixInverted);
                /*AcToolsLogging.Write(node.LocalMatrix);*/

                if (collectNodes) {
                    result.Add(node);
                }
            }

            return result;
        }

        [ContractAnnotation(@"collectNodes:true => notnull; collectNodes:false => null")]
        private static List<Kn5RenderableList> SetWheelNodeMatrix([NotNull] IRenderableObject parent, string namePostfix,
                Matrix wheelMatrix, Matrix suspMatrix,
                float wheelsPosition, float? wheelRadius, bool collectNodes) {
            return SetWheelNodeMatrix(parent, namePostfix, node => IsWheelNode(node) ?
                    GetWheelSpeedMatrix(wheelsPosition, wheelRadius ?? node.BoundingBox?.GetSize().Y / 2f ?? 0f) * wheelMatrix :
                    suspMatrix, collectNodes);
        }

        private void EnsureOriginalLocalMatricesSaved(string namePostfix) {
            // to make sure original matrices are saved
            foreach (var dummy in GetWheelNodesNames(this, namePostfix).Select(GetDummyByName).NonNull()) {
                _currentLodObject.GetOriginalLocalMatrix(dummy);
            }
        }

        private void EnsureOriginalRelativeToModelMatricesSaved(string namePostfix) {
            // to make sure original matrices are saved
            foreach (var dummy in GetWheelNodesNames(this, namePostfix).Select(GetDummyByName).NonNull()) {
                _currentLodObject.GetOriginalRelativeToModelMatrix(dummy);
            }
        }

        private void SteerWheel(bool left, [NotNull] CarData.SuspensionsPack pack, [CanBeNull] CarData.SuspensionBase suspension, float angle) {
            var axis = suspension?.WheelSteerAxis;
            if (axis == null) return;

            var namePostfix = left ? "LF" : "RF";
            UpdateModelMatrixInverted();
            EnsureOriginalRelativeToModelMatricesSaved(namePostfix);

            var range = (angle.Abs() / 30f).Saturate();
            angle += (left ? 1.5f : -1.5f) * MathF.Pow(range, 2f);

            axis = Tuple.Create(
                    pack.TranslateRelativeToCarModel(suspension, axis.Item1),
                    pack.TranslateRelativeToCarModel(suspension, axis.Item2));

            SetWheelNodeMatrix(this, namePostfix, node => GetSteerWheelMatrix(left, node, axis, -angle), false);
        }

        public class SteeringWheelParams {
            public Matrix ParentMatrix;
            public Matrix OriginalLocalMatrix;
            public float RotationDegress;
        }

        private float GetSteeringWheelRotationDegress(float offset) {
            if (!_steerLock.HasValue) {
                _steerLock = _carData.GetSteerLock();
            }

            return _steerLock.Value * offset;
        }

        [CanBeNull]
        private SteeringWheelParams GetSteeringWheelParams(float offset) {
            var node = GetDummyByName("STEER_HR");
            return node == null ? null : new SteeringWheelParams {
                OriginalLocalMatrix = node.OriginalNode.Transform.ToMatrix(),
                ParentMatrix = node.ParentMatrix,
                RotationDegress = GetSteeringWheelRotationDegress(offset)
            };
        }

        private float? _steerLock;

        private void SteerSteeringWheel(float offset) {
            UpdateModelMatrixInverted();

            var degress = GetSteeringWheelRotationDegress(offset);
            foreach (var node in new[] { "HR", "LR" }.Select(x => GetDummyByName($@"STEER_{x}")).NonNull()) {
                node.LocalMatrix = Matrix.RotationZ(degress.ToRadians()) * _currentLodObject.GetOriginalLocalMatrix(node);
            }
        }

        /// <summary>
        /// From -1 to 1.
        /// </summary>
        private float GetSteerOffset() {
            return (SteerDeg / 30f).Clamp(-1f, 1f);
        }

        private void ReloadSteeringWheelLock() {
            _steerLock = null;
            SteerSteeringWheel(GetSteerOffset());
        }

        private void ReupdatePreudoSteer() {
            _steerDegPrevious = float.NaN;
            UpdatePreudoSteer();
        }

        private void UpdatePreudoSteer() {
            var pack = SuspensionsPack;
            var front = pack.Front as CarData.IndependentSuspensionsGroup;
            if (front == null) return;

            var angle = SteerDeg;
            if (Equals(_steerDegPrevious, angle) && Equals(_wheelsPosPrevious, _wheelsPosition)) return;
            _steerDegPrevious = angle;
            _wheelsPosPrevious = _wheelsPosition;

            SteerWheel(true, pack, front.Left, angle);
            SteerWheel(false, pack, front.Right, angle);
            SteerSteeringWheel(GetSteerOffset());
            UpdateDriverSteerAnimation(GetSteerOffset());

            UpdateFrontWheelsShadowsRotation();
            _skinsWatcherHolder?.RaiseSceneUpdated();
        }
        #endregion

        private bool _alignWheelsByData;

        public bool AlignWheelsByData {
            get => _alignWheelsByData;
            set {
                if (Equals(value, _alignWheelsByData)) return;
                _alignWheelsByData = value;
                UpdateWheelsMatrices();
                OnPropertyChanged();
            }
        }

        #region Extra adjustments since it’s not very accurate
        public CarSuspensionModifiers SuspensionModifiers { get; }= new CarSuspensionModifiers();

        private void OnSuspensionModifiersChanged(object sender, PropertyChangedEventArgs e) {
            if (AlignWheelsByData) {
                UpdateWheelsMatrices();
            }
        }

        private float _wheelsSpeedKph;
        private float _wheelsPosition;

        public float WheelsSpeedKph {
            get => _wheelsSpeedKph;
            set {
                if (Equals(value, _wheelsSpeedKph)) return;
                _wheelsSpeedKph = value;
                OnPropertyChanged();
            }
        }

        private static Matrix GetWheelSpeedMatrix(float position, float radius) {
            return radius == 0f || position == 0f ? Matrix.Identity : Matrix.RotationX(position / radius);
        }

        private bool _blurredNodesBySpeedActive;

        private void SetBlurredBySpeed(float speedKph, float speedRad) {
            if (speedKph == 0f && !_blurredNodesBySpeedActive) return;

            if (speedKph == 0f && _blurredNodesActive == false) {
                _blurredNodesBySpeedActive = false;
                return;
            }

            _blurredNodesBySpeedActive = speedKph != 0f;

            speedKph = speedKph.Abs();
            speedRad = speedRad.Abs();

            var shouldBeBlurred = BlurredObjects.Any(x => x.MinSpeed > 0f && speedKph > x.MinSpeed);
            if (shouldBeBlurred != _blurredNodesActive) {
                _blurredNodesActive = shouldBeBlurred;
                OnPropertyChanged(nameof(BlurredNodesActive));
            }

            foreach (var o in CarData.BlurredObject.GetNamesToToggle(BlurredObjects, speedKph)) {
                var d = GetDummyByName(o.Item1);
                if (d != null) {
                    d.IsEnabled = o.Item2;
                }
            }

            if (_wheelsDesc == null) {
                var meshes = Meshes;
                for (var i = meshes.Count - 1; i >= 0; i--) {
                    meshes[i].DynamicMaterialParams.RadialSpeedBlur = 0f;
                }
            } else {
                foreach (var description in _wheelsDesc) {
                    var node = GetDummyByName($"WHEEL_{description.Name}");
                    if (node != null) {
                        foreach (var n in node.GetAllChildren().OfType<IKn5RenderableObject>()) {
                            n.DynamicMaterialParams.RadialSpeedBlur = (speedRad / 3f).Saturate();
                        }
                    }

                    node = GetDummyByName($"DISC_{description.Name}");
                    if (node != null) {
                        foreach (var n in node.GetAllChildren().OfType<IKn5RenderableObject>()) {
                            n.DynamicMaterialParams.RadialSpeedBlur = (speedRad / 3f).Saturate();
                        }
                    }
                }
            }
        }

        private bool OnTickWheels(float dt) {
            var speedRads = WheelsSpeedKph * 0.277778f;
            SetBlurredBySpeed(AlignWheelsByData ? WheelsSpeedKph : 0f, speedRads);

            if (WheelsSpeedKph == 0f || !AlignWheelsByData) return false;

            var wheels = _wheelsDesc;
            if (wheels == null) return false;

            _wheelsPosition += dt * speedRads;

            if (SteerDeg != 0f) {
                UpdatePreudoSteer();
            }

            for (var i = wheels.Length - 1; i >= 0; i--) {
                var wheel = wheels[i];
                if (SteerDeg == 0f || wheel != _wheelLfDesc && wheel != _wheelRfDesc) {
                    SetWheelNodeMatrix(this, wheel.Name,
                            GetWheelMatrix(wheel, false) * _wheelsFixMatrix,
                            GetWheelMatrix(wheel, true) * _wheelsFixMatrix,
                            _wheelsPosition, wheel.Radius, false);
                }
            }

            return true;
        }
        #endregion

        private static Matrix GetWheelMatrix(CarData.WheelDescription wheel, bool suspension) {
            if (wheel == null) return Matrix.Identity;

            var matrix = Matrix.Translation(suspension ? wheel.CenterSusp : wheel.CenterWheel);

            if (wheel.StaticToe != 0f) {
                matrix = Matrix.RotationY(-wheel.CenterWheel.X.Sign() * wheel.StaticToe.ToRadians()) * matrix;
            }

            if (wheel.StaticCamber != 0f) {
                matrix = Matrix.RotationZ(-wheel.CenterWheel.X.Sign() * wheel.StaticCamber.ToRadians()) * matrix;
            }

            return matrix;
        }

        private static bool IsWheelNode(RenderableList node) {
            return node.Name?.StartsWith("WHEEL_") == true;
        }

        public static bool SetWheelsByData([NotNull] IRenderableObject parent, IEnumerable<CarData.WheelDescription> wheels, Matrix graphicMatrix) {
            return wheels.Aggregate(false,
                    (current, wheel) => current | SetWheelNodeMatrix(parent, wheel.Name,
                            GetWheelMatrix(wheel, false) * graphicMatrix,
                            GetWheelMatrix(wheel, true) * graphicMatrix,
                            0f, 0f, true).Count > 0);
        }

        private List<string> _alignedWheelsNames;

        [CanBeNull]
        private CarData.WheelDescription[] _wheelsDesc;

        [CanBeNull]
        private CarData.WheelDescription _wheelLfDesc, _wheelRfDesc;

        private void UpdateWheelsMatrices() {
            var graphicMatrix = Matrix.Invert(_carData.GetGraphicMatrix());

            if (AlignWheelsByData) {
                UpdateModelMatrixInverted();

                _wheelsDesc = _carData.GetWheels(SuspensionModifiers).ToArray();
                _alignedWheelsNames = new List<string>();
                _wheelsFixMatrix = graphicMatrix;

                foreach (var wheel in _wheelsDesc) {
                    EnsureOriginalLocalMatricesSaved(wheel.Name);

                    if (wheel.IsFront) {
                        if (wheel.IsLeft) {
                            _wheelLfDesc = wheel;
                        } else {
                            _wheelRfDesc = wheel;
                        }
                    }

                    _alignedWheelsNames.AddRange(SetWheelNodeMatrix(this, wheel.Name,
                            GetWheelMatrix(wheel, false) * graphicMatrix,
                            GetWheelMatrix(wheel, true) * graphicMatrix,
                            _wheelsPosition, wheel.Radius, true)
                            .Select(x => x.Name));
                }

                if (_initiallyCalculatedPosition.HasValue) {
                    RootObject.LocalMatrix = _initiallyCalculatedPosition.Value;
                }

                AdjustPosition();
            } else if (_alignedWheelsNames != null) {
                _wheelsDesc = null;

                foreach (var name in _alignedWheelsNames) {
                    var node = GetDummyByName(name);
                    if (node == null) continue;

                    node.LocalMatrix = _currentLodObject.GetOriginalLocalMatrix(node);
                }

                _alignedWheelsNames = null;
                if (_initiallyCalculatedPosition.HasValue) {
                    RootObject.LocalMatrix = _initiallyCalculatedPosition.Value;
                }
            } else {
                return;
            }

            if (SteerDeg != 0f) {
                _steerDegPrevious = float.NaN;
                UpdatePreudoSteer();
            } else {
                UpdateFrontWheelsShadowsRotation();
            }

            UpdateRearWheelsShadowsRotation();
            _skinsWatcherHolder?.RaiseSceneUpdated();
        }

        private void OnRootObjectChangedWheels() {
            if (AlignWheelsByData) {
                UpdateWheelsMatrices();
            } else {
                ReupdatePreudoSteer();
            }
        }
    }
}