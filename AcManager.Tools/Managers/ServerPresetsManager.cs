using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Managers.Directories;
using AcManager.Tools.Objects;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Managers {
    public class ServerPresetsManager : AcManagerNew<ServerPresetObject>, IDisposable, ICreatingManager {
        private static ServerPresetsManager _instance;

        public static ServerPresetsManager Instance => _instance ?? (_instance = new ServerPresetsManager());

        private static readonly string[] WatchedFiles = {
            @"server_cfg.ini", @"entry_list.ini"
        };

        protected override bool ShouldSkipFile(string objectLocation, string filename) {
            if (base.ShouldSkipFile(objectLocation, filename)) return true;
            var inner = filename.SubstringExt(objectLocation.Length + 1);
            return !WatchedFiles.Contains(inner.ToLowerInvariant());
        }

        public static readonly string ServerDirectory;
        private static readonly string PresetsDirectory;
        private static readonly string DriversFilename;

        static ServerPresetsManager() {
            ServerDirectory = Path.Combine(AcRootDirectory.Instance.RequireValue, @"server");
            PresetsDirectory = Path.Combine(ServerDirectory, @"presets");
            DriversFilename = Path.Combine(ServerDirectory, @"manager", @"driverlist.ini");
            Directory.CreateDirectory(PresetsDirectory);
        }

        private readonly FileSystemWatcher _directoryWatcher;

        public ServerPresetsManager() {
            SavedDrivers = new ChangeableObservableCollection<ServerSavedDriver>(ServerSavedDriver.Load(DriversFilename));
            SavedDrivers.CollectionChanged += OnSavedDriversCollectionChanged;
            SavedDrivers.ItemPropertyChanged += OnSavedDriversItemPropertyChanged;

            var directory = Path.GetDirectoryName(DriversFilename);
            if (directory != null) {
                Directory.CreateDirectory(directory);

                _directoryWatcher = new FileSystemWatcher {
                    Path = directory,
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                    Filter = Path.GetFileName(DriversFilename),
                    EnableRaisingEvents = true,
                    IncludeSubdirectories = false
                };

                _directoryWatcher.Created += OnDirectoryWatcher;
                _directoryWatcher.Renamed += OnDirectoryWatcher;
                _directoryWatcher.Changed += OnDirectoryWatcher;
                _directoryWatcher.Deleted += OnDirectoryWatcher;
            }
        }

        private void OnSavedDriversCollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
            SaveDriversLater().Forget();
        }

        private void OnSavedDriversItemPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if (e.PropertyName == nameof(ServerSavedDriver.Deleted)) {
                SavedDrivers.Remove((ServerSavedDriver)sender);
            } else {
                SaveDriversLater().Forget();
            }
        }

        private bool _updating, _saving;

        private async Task SaveDriversLater() {
            if (_updating || _saving) return;
            _saving = true;

            try {
                await Task.Delay(300);
                if (_saving) {
                    SaveDrivers();
                    await Task.Delay(200);
                }
            } finally {
                _saving = false;
            }
        }

        private void SaveDrivers() {
            try {
                var file = new IniFile();
                foreach (var driver in SavedDrivers) {
                    driver.Save(file);
                }
                file.Save(DriversFilename);
            } catch (Exception e) {
                NonfatalError.Notify("Can’t save drivers list", e);
            }
        }

        private async void OnDirectoryWatcher(object sender, FileSystemEventArgs e) {
            if (_updating || _saving) return;
            _updating = true;

            try {
                await Task.Delay(300);
                if (_updating) {
                    ActionExtension.InvokeInMainThread(() => {
                        SavedDrivers.ReplaceIfDifferBy(ServerSavedDriver.Load(DriversFilename));
                    });
                    await Task.Delay(200);
                }
            } finally {
                _updating = false;
            }
        }

        public void StoreDriverEntry(ServerPresetDriverEntry entry) {
            var saved = SavedDrivers.FirstOrDefault(x => x.Guid == entry.Guid);
            if (saved != null) {
                saved.Extend(entry);
                SaveDriversLater().Forget();
            } else {
                SavedDrivers.Add(new ServerSavedDriver(entry));
            }
        }

        public override IAcDirectories Directories { get; } = new AcDirectories(PresetsDirectory);

        protected override ServerPresetObject CreateAcObject(string id, bool enabled) {
            return new ServerPresetObject(this, id, enabled);
        }

        public IAcObjectNew AddNew(string id = null) {
            if (id == null) {
                id = Directories.GetUniqueId("SERVER", "_{0:D2}", true, 0);
            }

            var directory = Directories.GetLocation(id, true);
            if (Directory.Exists(directory)) {
                throw new InformativeException("Can’t add a new object", "This ID is already taken.");
            }

            using (IgnoreChanges()) {
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory, "server_cfg.ini"), "");
                File.WriteAllText(Path.Combine(directory, "entry_list.ini"), "");

                var obj = CreateAndLoadAcObject(id, true);
                InnerWrappersList.Add(new AcItemWrapper(this, obj));
                UpdateList(true);

                return obj;
            }
        }

        public ChangeableObservableCollection<ServerSavedDriver> SavedDrivers { get; }

        public void Dispose() {
            _directoryWatcher.EnableRaisingEvents = false;
            _directoryWatcher.Dispose();
        }
    }
}
