using System;
using System.IO;
using System.Threading.Tasks;
using AcTools.Kn5File;
using AcTools.KnhFile;
using AcTools.Render.Base;
using AcTools.Render.Base.Cameras;
using AcTools.Render.Base.Objects;
using AcTools.Render.Base.Utils;
using AcTools.Render.Kn5Specific.Animations;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;
using SlimDX;

#pragma warning disable 169

namespace AcTools.Render.Kn5Specific.Objects {
    public partial class Kn5RenderableCar {
        #region Driver
        private bool _driverSet;
        private string _driverModelFilename;
        private IDisposable _driverModelWatcher;
        private string _driverHierarchyFilename;
        private IDisposable _driverHierarchyWatcher;

        [CanBeNull]
        private Kn5RenderableDriver _driver;

        [CanBeNull]
        private string _driverSteerFilename;

        [CanBeNull]
        private Lazier<KsAnimAnimator> _driverSteerAnimator;

        private float _driverSteerLock;

        private void ResetDriver() {
            _driverSet = false;
            DisposeHelper.Dispose(ref _driverModelWatcher);
            UnloadDriverModel();
        }

        private void UnloadDriverModel() {
            Remove(_driver);
            DisposeHelper.Dispose(ref _driver);
            _driverSteerAnimator = null;
            ObjectsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void LoadDriverModel() {
            if (_driver != null) return;

            var driverDescription = _carData.GetDriverDescription();
            if (driverDescription == null || !File.Exists(_driverModelFilename)) return;

            var driver = new Kn5RenderableDriver(Kn5.FromFile(_driverModelFilename), Matrix.Translation(driverDescription.Offset),
                    _currentSkin == null ? null : Path.Combine(_skinsDirectory, _currentSkin),
                    AsyncTexturesLoading, _asyncOverrideTexturesLoading, _converter) {
                        LiveReload = LiveReload,
                        MagickOverride = MagickOverride
                    };
            _driver = driver;

            if (File.Exists(_driverHierarchyFilename)) {
                driver.AlignNodes(Knh.FromFile(_driverHierarchyFilename));
            }

            _driverSteerAnimator = Lazier.Create(() => {
                var animator = CreateAnimator(RootDirectory, driverDescription.SteerAnimation,
                        clampEnabled: false, skipFixed: false);
                if (animator == null) return null;

                animator.Reset += OnSteerAnimatorReset;
                return animator;
            });
            _driverSteerLock = driverDescription.SteerAnimationLock;

            driver.LocalMatrix = RootObject.LocalMatrix;
            Add(driver);
            ObjectsChanged?.Invoke(this, EventArgs.Empty);

            if (_steerDeg != 0 || OptionFixKnh) {
                UpdateDriverSteerAnimation(GetSteerOffset());
            }

            if (DebugMode) {
                driver.DebugMode = true;
            }
        }

        private void OnSteerAnimatorReset(object sender, EventArgs eventArgs) {
            _driver?.RealignNodes();
        }

        private bool _reloading;

        private async void ReloadDriverModel() {
            if (_reloading) return;
            _reloading = true;

            try {
                await Task.Delay(300);
                UnloadDriverModel();
                LoadDriverModel();
            } finally {
                _reloading = false;
            }
        }

        private void InitializeDriver() {
            if (_driverSet) return;
            _driverSet = true;

            if (_driverHierarchyFilename == null) {
                _driverHierarchyFilename = Path.Combine(RootDirectory, "driver_base_pos.knh");
                _driverHierarchyWatcher = SimpleDirectoryWatcher.WatchFile(_driverHierarchyFilename, () => {
                    _driver?.AlignNodes(Knh.FromFile(_driverHierarchyFilename));
                });
            }

            var driver = _carData.GetDriverDescription();
            if (driver == null) return;

            var contentDirectory = Path.GetDirectoryName(Path.GetDirectoryName(RootDirectory));
            if (contentDirectory == null) return;

            var driversDirectory = Path.Combine(contentDirectory, "driver");
            _driverModelFilename = Path.Combine(driversDirectory, driver.Name + ".kn5");
            _driverModelWatcher = SimpleDirectoryWatcher.WatchFile(_driverModelFilename, ReloadDriverModel);
            LoadDriverModel();

            ObjectsChanged?.Invoke(this, EventArgs.Empty);
        }

        private bool _useUp;

        public bool UseUp {
            get => _useUp;
            set {
                if (value == _useUp) return;
                _useUp = value;
                OnPropertyChanged();
            }
        }

        private Up _up;

        private void UpdateDriverSteerAnimation(float offset) {
            if (_driver == null) return;

            if (UseUp) {
                if (_up == null) {
                    _up = new Up(_driver, GetDummyByName("STEER_HR"));
                }

                _up.Update(offset, GetSteeringWheelParams(offset));
            } else {
                // one animation is 720°
                var steerLock = _steerLock ?? 360;
                var steer = offset * steerLock / _driverSteerLock;
                _driverSteerAnimator?.Value?.SetImmediate(_driver.RootObject, 0.5f - steer / 2f, _skinsWatcherHolder);

                if (_driverShiftAnimator?.IsSet == true) {
                    _driverShiftAnimator.Value?.Update();
                }
            }
        }

        private void DrawDriver(IDeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
            if (!_isDriverVisible) return;
            InitializeDriver();
            _driver?.Draw(contextHolder, camera, mode);
            _up?.Draw(contextHolder, camera, mode);
        }

        internal string DebugString => _up?.DebugString;
        private bool _isDriverVisible;

        public bool IsDriverVisible {
            get => _isDriverVisible;
            set {
                if (Equals(value, _isDriverVisible)) return;
                _isDriverVisible = value;
                OnPropertyChanged();

                if (_driver == null) return;
                if (!value) {
                    Remove(_driver);
                } else {
                    Add(_driver);
                }

                ObjectsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        #endregion

        #region Shift
        private Lazier<KsAnimAnimator> _driverShiftAnimator;
        private Lazier<KsAnimAnimator> _carShiftAnimator;

        private void InitializeShiftAnimation() {
            if (_driverShiftAnimator == null) {
                _driverShiftAnimator = Lazier.Create(() => {
                    var result = CreateAnimator(RootDirectory, "shift.ksanim", skipFixed: false);
                    if (result == null) return null;

                    result.PingPongMode = true;
                    return result;
                });
            }

            if (_carShiftAnimator == null) {
                _carShiftAnimator = Lazier.Create(() => {
                    var result = CreateAnimator(RootDirectory, "car_shift.ksanim");
                    if (result == null) return null;

                    result.PingPongMode = true;
                    _driverShiftAnimator.Value?.Linked.Add(result);
                    return result;
                });
            }
        }

        [CanBeNull]
        private RenderableList GetShiftingHand() {
            var driver = _carData.GetDriverDescription();
            return _driver?.GetDummyByName(driver?.ShiftInvertHands == true ? "DRIVER:RIG_Clave_L" : "DRIVER:RIG_Clave_R");
        }

        private void ReenableShiftAnimation() {
            if (_driverShiftAnimator?.IsSet == true && _driver != null) {
                var shiftingHand = GetShiftingHand();
                if (shiftingHand == null) return;

                _carShiftAnimator.Value?.SetParent(RootObject, _skinsWatcherHolder);
                if (_shiftAnimationEnabled) {
                    _driverShiftAnimator.Value?.Loop(shiftingHand);
                } else {
                    _driverShiftAnimator.Value?.SetTarget(shiftingHand, 0f, _skinsWatcherHolder);
                }
            }
        }

        private bool _shiftAnimationEnabled;

        public bool ShiftAnimationEnabled {
            get => _shiftAnimationEnabled;
            set {
                if (Equals(value, _shiftAnimationEnabled)) return;
                _shiftAnimationEnabled = value;
                _skinsWatcherHolder?.RaiseSceneUpdated();
                OnPropertyChanged();

                InitializeShiftAnimation();
                var shiftingHand = GetShiftingHand();
                if (shiftingHand == null) return;

                _carShiftAnimator.Value?.SetParent(RootObject, _skinsWatcherHolder);
                if (value) {
                    _driverShiftAnimator.Value?.Loop(shiftingHand);
                } else if (_driverShiftAnimator.Value?.Position > 0.5) {
                    _driverShiftAnimator.Value?.Loop(shiftingHand, 1);
                } else {
                    _driverShiftAnimator.Value?.SetTarget(shiftingHand, 0f, _skinsWatcherHolder);
                }
            }
        }

        public bool HasShiftAnimation {
            get {
                InitializeShiftAnimation();
                return _driverShiftAnimator.Value != null;
            }
        }
        #endregion

        #region Crew
        private bool _crewSet;

        [CanBeNull]
        private Kn5RenderableSkinnable _crewMain;

        [CanBeNull]
        private Kn5RenderableSkinnable _crewTyres;

        [CanBeNull]
        private Kn5RenderableSkinnable _crewStuff;

        private Lazier<KsAnimAnimator> _crewAnimator;

        private void InitializeCrewMain() {
            var contentDirectory = Path.GetDirectoryName(Path.GetDirectoryName(RootDirectory));
            if (contentDirectory == null) return;

            var driversDirectory = Path.Combine(contentDirectory, "objects3D");
            var filename = Path.Combine(driversDirectory, "pitcrew.kn5");
            if (!File.Exists(filename)) return;

            _crewMain = new Kn5RenderableSkinnable(Kn5.FromFile(filename), Matrix.RotationY(MathF.PI) * Matrix.Translation(-1.6f, 0f, 2f),
                    _currentSkin == null ? null : Path.Combine(_skinsDirectory, _currentSkin),
                    AsyncTexturesLoading, _asyncOverrideTexturesLoading, _converter) {
                        LiveReload = LiveReload,
                        MagickOverride = MagickOverride
                    };

            _crewAnimator = Lazier.Create(() => CreateAnimator(Path.Combine(driversDirectory, "pitcrew_idle_dw.ksanim"), 10f));
            _crewAnimator.Value?.Loop(_crewMain.RootObject);

            Add(_crewMain);
        }

        private void InitializeCrewTyres() {
            var contentDirectory = Path.GetDirectoryName(Path.GetDirectoryName(RootDirectory));
            if (contentDirectory == null) return;

            var driversDirectory = Path.Combine(contentDirectory, "objects3D");
            var filename = Path.Combine(driversDirectory, "pitcrewtyre.kn5");
            if (!File.Exists(filename)) return;

            _crewTyres = new Kn5RenderableSkinnable(Kn5.FromFile(filename), Matrix.RotationY(-MathF.PI * 0.6f) * Matrix.Translation(1.9f, 0f, 0.8f),
                    _currentSkin == null ? null : Path.Combine(_skinsDirectory, _currentSkin),
                    AsyncTexturesLoading, _asyncOverrideTexturesLoading, _converter) {
                        LiveReload = LiveReload,
                        MagickOverride = MagickOverride
                    };

            Add(_crewTyres);
        }

        private async Task InitializeCrewStuff() {
            var data = await ExtraModels.GetAsync(ExtraModels.KeyCrewExtra);
            if (data == null) return;

            _crewStuff = new Kn5RenderableSkinnable(Kn5.FromBytes(data), Matrix.RotationY(-MathF.PI * 0.5f) * Matrix.Translation(0.09f, 0f, 0.08f),
                    _currentSkin == null ? null : Path.Combine(_skinsDirectory, _currentSkin),
                    AsyncTexturesLoading, _asyncOverrideTexturesLoading, _converter) {
                        LiveReload = LiveReload,
                        MagickOverride = MagickOverride
                    };

            if (!_isCrewVisible) return;
            Add(_crewStuff);
            ObjectsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void InitializeCrew() {
            if (_crewSet) return;
            _crewSet = true;

            InitializeCrewMain();
            InitializeCrewTyres();
            InitializeCrewStuff().Ignore();

            ObjectsChanged?.Invoke(this, EventArgs.Empty);
            UpdateCrewDebugMode();
        }

        private void UpdateCrewDebugMode() {
            if (_crewMain != null) {
                _crewMain.DebugMode = DebugMode;
            }

            if (_crewTyres != null) {
                _crewTyres.DebugMode = DebugMode;
            }

            if (_crewStuff != null) {
                _crewStuff.DebugMode = DebugMode;
            }
        }

        private void UpdateCrewParams() {
            if (_crewMain != null) {
                _crewMain.LiveReload = LiveReload;
                _crewMain.MagickOverride = MagickOverride;
            }

            if (_crewTyres != null) {
                _crewTyres.LiveReload = LiveReload;
                _crewTyres.MagickOverride = MagickOverride;
            }

            if (_crewStuff != null) {
                _crewStuff.LiveReload = LiveReload;
                _crewStuff.MagickOverride = MagickOverride;
            }
        }

        private void DrawCrew(IDeviceContextHolder contextHolder, ICamera camera, SpecialRenderMode mode) {
            if (!_isCrewVisible) return;
            InitializeCrew();
            _crewMain?.Draw(contextHolder, camera, mode);
            _crewTyres?.Draw(contextHolder, camera, mode);
            _crewStuff?.Draw(contextHolder, camera, mode);
        }

        private bool _isCrewVisible;

        public bool IsCrewVisible {
            get => _isCrewVisible;
            set {
                if (Equals(value, _isCrewVisible)) return;
                _isCrewVisible = value;
                OnPropertyChanged();

                if (_crewMain != null) {
                    if (!value) {
                        Remove(_crewMain);
                        Remove(_crewTyres);
                        Remove(_crewStuff);
                    } else {
                        if (_crewMain != null) Add(_crewMain);
                        if (_crewTyres != null) Add(_crewTyres);
                        if (_crewStuff != null) Add(_crewStuff);
                    }
                }

                ObjectsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        #endregion
    }
}