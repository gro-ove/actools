using System;
using System.Collections.Generic;
using AcManager.Tools.AcManagersNew;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Managers {
    public class Superintendent {
        public static Superintendent Instance { get; private set; }

        public static Superintendent Initialize() {
            if (Instance != null) throw new Exception("already initialized");
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
                FontsManager.Initialize(),
                ReplaysManager.Initialize(),
                KunosCareerManager.Initialize()
            };

            if (IsReady) {
                RescanManagers();
            }
        }

        private void RescanManagers() {
            var start = DateTime.Now;
            foreach (var manager in _managers) {
                manager.Rescan();
            }
            Logging.Write("SUPERINTENDENT: rescanning finished: {0} managers, {1}", _managers.Count, DateTime.Now - start);
        }

        void AcRootDirectory_Changed(object sender, AcRootDirectoryEventArgs e) {
            RescanManagers();
        }

        public bool IsReady => AcRootDirectory.Instance.IsReady;
    }
}
