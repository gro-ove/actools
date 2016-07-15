using System;
using System.Collections.Generic;
using System.Diagnostics;
using AcManager.Tools.AcManagersNew;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Managers {
    public class Superintendent {
        public static Superintendent Instance { get; private set; }

        public static Superintendent Initialize() {
            if (Instance != null) throw new Exception("Already initialized");
            return Instance = new Superintendent();
        }

        private readonly IReadOnlyList<IAcManagerNew> _managers;

        public Superintendent() {
            AcRootDirectory.Initialize();
            AcRootDirectory.Instance.Changed += AcRootDirectory_Changed;

            _managers = new IAcManagerNew[] {
                CarsManager.Initialize(),
                TracksManager.Initialize(),
                ShowroomsManager.Initialize(),
                WeatherManager.Initialize(),
                PpFiltersManager.Initialize(),
                PythonAppsManager.Initialize(),
                FontsManager.Initialize(),
                ReplaysManager.Initialize(),
                KunosCareerManager.Initialize()
            };

            if (IsReady) {
                RescanManagers();
            }
        }

        private void RescanManagers() {
            var w = Stopwatch.StartNew();
            foreach (var manager in _managers) {
                manager.Rescan();
            }

            Logging.Write($"[Superintendent] Rescanning finished: {_managers.Count} managers, {w.Elapsed.TotalMilliseconds:F2} ms");
        }

        void AcRootDirectory_Changed(object sender, AcRootDirectoryEventArgs e) {
            RescanManagers();
        }

        public bool IsReady => AcRootDirectory.Instance.IsReady;
    }
}
