using System;

namespace AcTools.Processes {
    public partial class Showroom {
        [Obsolete]
        public static string Shot(string acRoot, string carName, string showroomName, bool manualMode,
                double dx, double dy, double distance, string filter, bool disableSweetFx, bool slowMode) {
            var s = new ClassicShooter {
                AcRoot = acRoot,
                CarId = carName,
                ShowroomId = showroomName,
                UseBmp = true,
                DisableWatermark = true,
                DisableSweetFx = disableSweetFx,
                Filter = filter
            };

            s.SetRotate(dx, dy);
            s.SetDistance(distance);

            if (slowMode) {
                s.SlowMode();
            }

            try {
                s.ShotAll(manualMode);
                return s.OutputDirectory;
            } finally {
                s.Dispose();
            }
        }

        [Obsolete]
        public static string ShotAll(string acRoot, string carName, string showroomName, string cameraPosition, string cameraLookAt, double cameraFov,
                string filter, bool disableSweetFx) {
            var s = new KunosShotter {
                AcRoot = acRoot,
                CarId = carName,
                ShowroomId = showroomName,
                UseBmp = true,
                DisableWatermark = true,
                DisableSweetFx = disableSweetFx,
                Filter = filter
            };

            s.SetCamera(cameraPosition, cameraLookAt, cameraFov, 0d);

            try {
                s.ShotAll();
                return s.OutputDirectory;
            } finally {
                s.Dispose();
            }
        }

        [Obsolete]
        public static string ShotOne(string acRoot, string carName, string showroomName, string skinName, string cameraPosition, string cameraLookAt, double cameraFov,
                string filter, bool disableSweetFx) {
            var s = new KunosShotter {
                AcRoot = acRoot,
                CarId = carName,
                ShowroomId = showroomName,
                UseBmp = true,
                DisableWatermark = true,
                DisableSweetFx = disableSweetFx,
                Filter = filter
            };

            s.SetCamera(cameraPosition, cameraLookAt, cameraFov, 0d);

            try {
                s.Shot(skinName);
                return s.OutputDirectory;
            } finally {
                s.Dispose();
            }
        }
    }
}
