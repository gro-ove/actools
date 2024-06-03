using System;
using System.IO;
using System.Windows.Input;
using AcTools.DataFile;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Data;
using AcTools.Utils.Helpers;
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

        public ICommand ResetDriverMovementCommand
            => _resetDriverMovementCommand ?? (_resetDriverMovementCommand = new DelegateCommand(() => { _driver?.ResetMovement(); }));

        private ICommand _saveDriverMovementCommand;

        public ICommand SaveDriverMovementCommand => _saveDriverMovementCommand ?? (_saveDriverMovementCommand = new DelegateCommand(() => {
            try {
                _driver?.SaveMovement(Path.Combine(RootDirectory, "animations",
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

        public void DrawMovementArrows(DeviceContextHolder holder, CameraBase camera, bool drawMain) {
            Movable.ParentMatrix = Matrix;
            if (drawMain) {
                Movable.Draw(holder, camera, SpecialRenderMode.Simple);
            }
            if (IsDriverMovable) {
                _driver?.DrawMovementArrows(holder, camera);
            }
            if (SuspensionDebug) {
                DrawSuspensionMovementArrows(holder, camera);
            }
            if (AreWingsVisible) {
                _wingsLines?.DrawMovementArrows(this, holder, camera);
            }
            if (AreFlamesVisible) {
                _flamesLines?.DrawMovementArrows(this, holder, camera);
            }
            if (IsFuelTankVisible) {
                _fuelTankLines?.DrawMovementArrows(this, holder, camera);
            }
        }

        private Matrix? _originalPosition;

        public void ResetPosition() {
            if (_originalPosition.HasValue) {
                LocalMatrix = _originalPosition.Value;
            }
            _driver?.ResetPosition();
        }

        private bool _dataObjectMoved;

        public bool DataObjectMoved {
            get => _dataObjectMoved;
            set {
                if (value == _dataObjectMoved) return;
                _dataObjectMoved = value;
                OnPropertyChanged();
            }
        }

        public void SaveMovedDataObjects(string dataDirectory) {
            var graphicMatrix = _carData.GetGraphicMatrix();

            if (_suspensionMovables != null) {
                var iniFile = new IniFile(Path.Combine(dataDirectory, "suspensions.ini"));
                foreach (var m in _suspensionMovables) {
                    m.Save(iniFile);
                }
                iniFile.Save();
            }

            if (_wingsLines != null) {
                var iniFile = new IniFile(Path.Combine(dataDirectory, "aero.ini"));
                foreach (var t in _wingsLines.GetTransforms()) {
                    iniFile[t.OriginalName].SetSlimVector3("POSITION", Vector3.TransformCoordinate(Vector3.Zero, t.Transform * graphicMatrix));
                }
                iniFile.Save();
            }

            if (_flamesLines != null) {
                var iniFile = new IniFile(Path.Combine(dataDirectory, "flames.ini"));
                foreach (var t in _flamesLines.GetTransforms()) {
                    if (iniFile[t.Name].Count == 0) {
                        foreach (var pair in iniFile[t.OriginalName]) {
                            iniFile[t.Name].Set(pair.Key, pair.Value);
                        }
                    }
                    iniFile[t.Name].SetSlimVector3("POSITION", Vector3.TransformCoordinate(Vector3.Zero, t.Transform));
                    iniFile[t.Name].SetSlimVector3("DIRECTION", Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitZ, t.Transform)));
                }
                iniFile.Save();
            }

            if (_fuelTankLines != null) {
                var iniFile = new IniFile(Path.Combine(dataDirectory, "car.ini"));
                foreach (var t in _fuelTankLines.GetTransforms()) {
                    iniFile["FUELTANK"].SetSlimVector3("POSITION", Vector3.TransformCoordinate(Vector3.Zero, t.Transform * graphicMatrix));
                }
                iniFile.Save();
            }

            if (!_carData.IsPacked) {
                ResetMovedDataObjects();
            }
        }

        public void ResetMovedDataObjects() {
            _suspensionsPack = null;
            _suspensionMovables?.DisposeEverything();
            _suspensionMovables = null;
            DataObjectMoved = false;
            OnPropertyChanged(nameof(SuspensionsPack));
        }

        public bool MoveObject(Vector2 relativeFrom, Vector2 relativeDelta, CameraBase camera, bool tryToClone) {
            if (Movable.MoveObject(relativeFrom, relativeDelta, camera, false, out _)
                    || IsDriverMovable && _driver?.Movable.MoveObject(relativeFrom, relativeDelta, camera, false, out _) == true) {
                return true;
            }

            if (MoveSuspension(relativeFrom, relativeDelta, camera)) {
                DataObjectMoved = true;
                return true;
            }

            if (AreWingsVisible && _wingsLines?.MoveObject(relativeFrom, relativeDelta, camera, tryToClone, out _) == true
                    || AreFlamesVisible && _flamesLines?.MoveObject(relativeFrom, relativeDelta, camera, tryToClone, out _) == true
                    || IsFuelTankVisible && _fuelTankLines?.MoveObject(relativeFrom, relativeDelta, camera, tryToClone, out _) == true) {
                DataObjectMoved = true;
                return true;
            }

            return false;
        }

        public void StopMovement() {
            Movable.StopMovement();
            if (IsDriverMovable) {
                _driver?.Movable.StopMovement();
            }
            if (SuspensionDebug) {
                StopSuspensionMovement();
            }
            if (AreWingsVisible) {
                _wingsLines?.StopMovement();
            }
            if (AreFlamesVisible) {
                _flamesLines?.StopMovement();
            }
            if (IsFuelTankVisible) {
                _fuelTankLines?.StopMovement();
            }
        }

        void IMoveable.Move(Vector3 delta) {
            if (!_originalPosition.HasValue) {
                _originalPosition = LocalMatrix;
            }

            LocalMatrix *= Matrix.Translation(delta);
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