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

                    // engine
                    LoadEngineParams();

                    // sounds
                    ResetSoundEmitters();

                    // driver
                    ResetDriver();

                    // debug lines
                    _wingsLines.Reset();
                    _fuelTankLines.Reset();
                    _colliderLines.Reset();
                    _flamesLines.Reset();
                    _wheelsLines.Reset();
                    _inertiaBoxLines.Reset();
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
                    _inertiaBoxLines.Reset();
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
                    ResetDriver();
                    break;
                case "engine.ini":
                    LoadEngineParams();
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
                case "sounds.ini":
                    ResetSoundEmitters();
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
            OnPropertyChanged(nameof(SuspensionsPack));
        }
    }
}