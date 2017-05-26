// #define BB_PERF_PROFILE

using System;
using AcTools.DataFile;
using AcTools.Utils.Helpers;

namespace AcTools.Render.Kn5Specific.Objects {
    public partial class Kn5RenderableCar {
        private void OnDataChanged(object sender, DataChangedEventArgs e) {
            var holder = _skinsWatcherHolder;
            if (holder == null) return;

            switch (e.PropertyName) {
                case null:
                    ResetAmbientShadowSize();
                    ReloadSteeringWheelLock();
                    CamerasChanged?.Invoke(this, EventArgs.Empty);
                    ExtraCamerasChanged?.Invoke(this, EventArgs.Empty);
                    ResetExtras();
                    ResetLights();
                    LoadMirrors(holder);
                    ReloadSuspension();
                    ResetWings();

                    // driver
                    _driverSet = false;
                    _driverSteerAnimator = null;
                    DisposeHelper.Dispose(ref _driver);

                    // debug lines
                    _wingsLines.Reset();
                    _fuelTankLines.Reset();
                    _colliderLines.Reset();
                    _flamesLines.Reset();
                    _wheelsLines.Reset();
                    break;

                case "aero.ini":
                    _wingsLines.Reset();
                    break;
                case "ambient_shadows.ini":
                    ResetAmbientShadowSize();
                    break;
                case "car.ini":
                    _fuelTankLines.Reset();
                    _wheelsLines.Reset();
                    _colliderLines.Reset(); // because they are affected by offset
                    ReloadSteeringWheelLock();
                    CamerasChanged?.Invoke(this, EventArgs.Empty);
                    if (AlignWheelsByData) {
                        UpdateWheelsMatrices();
                    }
                    break;
                case "cameras.ini":
                    ExtraCamerasChanged?.Invoke(this, EventArgs.Empty);
                    break;
                case "colliders.ini":
                    _colliderLines.Reset();
                    break;
                case "dash_cam.ini":
                    CamerasChanged?.Invoke(this, EventArgs.Empty);
                    break;
                case "driver3d.ini":
                    _driverSet = false;
                    DisposeHelper.Dispose(ref _driver);
                    _driverSteerAnimator = null;
                    break;
                case "extra_animations.ini":
                    ResetExtras();
                    break;
                case "flames.ini":
                    _flamesLines.Reset();
                    break;
                case "lights.ini":
                    ResetLights();
                    break;
                case "mirrors.ini":
                    LoadMirrors(holder);
                    break;
                case "suspensions.ini":
                    ReloadSuspension();
                    _wheelsLines.Reset();
                    _wheelsDesc = null;
                    if (AlignWheelsByData) {
                        UpdateWheelsMatrices();
                    }
                    break;
                case "tyres.ini":
                    _wheelsLines.Reset();
                    _wheelsDesc = null;
                    if (AlignWheelsByData) {
                        UpdateWheelsMatrices();
                    }
                    break;
                case "wing_animations.ini":
                    ResetWings();
                    break;
            }

            holder.RaiseUpdateRequired();
        }

        private void ReloadSuspension() {
            _suspensionsPack = null;
            DisposeHelper.Dispose(ref _suspensionLines);
            ReupdatePreudoSteer();
        }
    }
}