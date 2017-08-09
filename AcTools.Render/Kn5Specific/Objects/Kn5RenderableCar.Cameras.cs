using System;
using System.Linq;
using AcTools.Render.Base.Cameras;
using JetBrains.Annotations;

namespace AcTools.Render.Kn5Specific.Objects {
    public partial class Kn5RenderableCar {
        public event EventHandler CamerasChanged;
        public event EventHandler ExtraCamerasChanged;

        [CanBeNull]
        public CameraBase GetDriverCamera() {
            return _carData.GetDriverCamera()?.ToCamera(Matrix);
        }

        [CanBeNull]
        public CameraBase GetDashboardCamera() {
            return _carData.GetDashboardCamera()?.ToCamera(Matrix);
        }

        [CanBeNull]
        public CameraBase GetBonnetCamera() {
            return _carData.GetBonnetCamera()?.ToCamera(Matrix);
        }

        [CanBeNull]
        public CameraBase GetBumperCamera() {
            return _carData.GetBumperCamera()?.ToCamera(Matrix);
        }

        public int GetCamerasCount() {
            return _carData.GetExtraCameras().Count();
        }

        [CanBeNull]
        public CameraBase GetCamera(int index) {
            return _carData.GetExtraCameras().Skip(index).Select(x => x.ToCamera(Matrix)).FirstOrDefault();
        }
    }
}