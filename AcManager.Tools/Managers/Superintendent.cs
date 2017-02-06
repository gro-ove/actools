using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using AcManager.Tools.AcManagersNew;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Managers {
    public class Superintendent {
        public static Superintendent Instance { get; private set; }

        public static Superintendent Initialize() {
            if (Instance != null) throw new Exception("Already initialized");
            Instance = new Superintendent();
            Instance.InnerInitialize();
            return Instance;
        }

        public Superintendent() {
            if (AcRootDirectory.Instance == null) {
                AcRootDirectory.Initialize();
                if (AcRootDirectory.Instance == null) return;
            }

            AcRootDirectory.Instance.Changed += AcRootDirectory_Changed;
        }

        private IReadOnlyList<IAcManagerNew> _managers;

        private void InnerInitialize() {
            _managers = new IAcManagerNew[] {
                CarsManager.Initialize(),
                TracksManager.Initialize()
            };

            if (IsReady) {
                RescanManagers();
            }
        }

        public class ClosingEventArgs : EventArgs {
            private readonly List<string> _list = new List<string>();

            public IReadOnlyList<string> UnsavedDisplayNames => _list;

            public void Add(string displayName) {
                _list.Add(displayName);
            }
        }

        public event EventHandler<ClosingEventArgs> Closing;

        public IReadOnlyList<string> UnsavedChanges() {
            var args = new ClosingEventArgs();
            Logging.Debug(args);
            Closing?.Invoke(this, args);
            Logging.Debug(args.UnsavedDisplayNames.JoinToString(";"));
            return args.UnsavedDisplayNames;
        }

        public event EventHandler SavingAll;

        public void SaveAll() {
            SavingAll?.Invoke(this, EventArgs.Empty);
        }

        private void RescanManagers() {
            var w = Stopwatch.StartNew();
            foreach (var manager in _managers) {
                manager.Rescan();
            }

            Logging.Write($"Rescanning finished: {_managers.Count} managers, {w.Elapsed.TotalMilliseconds:F2} ms");
        }

        void AcRootDirectory_Changed(object sender, AcRootDirectoryEventArgs e) {
            RescanManagers();
        }

        public bool IsReady => AcRootDirectory.Instance.IsReady;
    }
}
