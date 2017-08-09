using System;
using System.IO;
using System.Windows.Input;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using SlimDX;

namespace AcTools.Render.Kn5Specific.Objects {
    public partial class Kn5RenderableCar : IMoveable {
        private bool _isDriverMovable;

        public bool IsDriverMovable {
            get => _isDriverMovable;
            set {
                if (Equals(value, _isDriverMovable)) return;
                _isDriverMovable = value;
                OnPropertyChanged();
                _driver?.Movable.StopMovement();
            }
        }

        private ICommand _resetDriverMovementCommand;

        public ICommand ResetDriverMovementCommand => _resetDriverMovementCommand ?? (_resetDriverMovementCommand = new DelegateCommand(() => {
            _driver?.ResetMovement();
        }));

        private ICommand _saveDriverMovementCommand;

        public ICommand SaveDriverMovementCommand => _saveDriverMovementCommand ?? (_saveDriverMovementCommand = new DelegateCommand(() => {
            try {
                _driver?.SaveMovement(Path.Combine(_rootDirectory, "animations",
                        _carData.GetDriverDescription()?.SteerAnimation ?? throw new Exception("Failed to load driver data")));
            } catch (Exception e) {
                AcToolsLogging.NonFatalErrorNotify("Can’t save new “steer.ksanim”", null, e);
            }
        }));

        private ICommand _updateDriverKnhCommand;

        public ICommand UpdateDriverKnhCommand => _updateDriverKnhCommand ?? (_updateDriverKnhCommand = new DelegateCommand(() => {
            try {
                _driver?.UpdateKnh();
            } catch (Exception e) {
                AcToolsLogging.NonFatalErrorNotify("Can’t save new “driver_base_pos.knh”", null, e);
            }
        }));

        private MoveableHelper _movable;
        private MoveableHelper Movable => _movable ?? (_movable = new MoveableHelper(this));

        public void DrawMovementArrows(DeviceContextHolder holder, CameraBase camera) {
            Movable.ParentMatrix = Matrix;
            Movable.Draw(holder, camera, SpecialRenderMode.Simple);
            if (IsDriverMovable) {
                _driver?.DrawMovementArrows(holder, camera);
            }
        }

        private Matrix? _originalPosition;

        public void ResetPosition() {
            if (_originalPosition.HasValue) {
                LocalMatrix = _originalPosition.Value;
            }

            _driver?.ResetPosition();
        }

        public bool MoveObject(Vector2 relativeFrom, Vector2 relativeDelta, CameraBase camera, bool tryToClone) {
            return Movable.MoveObject(relativeFrom, relativeDelta, camera, tryToClone, out var c) ||
                    IsDriverMovable && _driver?.Movable.MoveObject(relativeDelta, relativeDelta, camera, tryToClone, out c) == true;
        }

        public void StopMovement() {
            Movable.StopMovement();
            if (IsDriverMovable) {
                _driver?.Movable.StopMovement();
            }
        }

        void IMoveable.Move(Vector3 delta) {
            if (!_originalPosition.HasValue) {
                _originalPosition = LocalMatrix;
            }

            LocalMatrix = LocalMatrix * Matrix.Translation(delta);
            UpdateBoundingBox();
        }

        void IMoveable.Rotate(Quaternion delta) {
            if (!_originalPosition.HasValue) {
                _originalPosition = LocalMatrix;
            }

            LocalMatrix = Matrix.RotationQuaternion(delta) * LocalMatrix;
            UpdateBoundingBox();
        }

        void IMoveable.Scale(Vector3 delta) {
            // not supported
        }

        IMoveable IMoveable.Clone() {
            return null;
        }
    }
}