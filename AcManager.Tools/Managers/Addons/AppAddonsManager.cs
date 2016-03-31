using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Lists;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Managers.Addons {
    public class AppAddonsManager {
        public static AppAddonsManager Instance { get; private set; }

        public static AppAddonsManager Initialize(string dir) {
            if (Instance != null) throw new Exception("already initialized");
            return Instance = new AppAddonsManager(dir);
        }

        public readonly string AddonsDirectory;
        public readonly BetterObservableCollection<AppAddonInformation> List;

        private bool _locallyLoaded;

        public event AddonEventHandler AddonEnabled;
        public event AddonEventHandler AddonDisabled;

        [CanBeNull]
        public AppAddonInformation GetById(string addonId) {
            return List.GetByIdOrDefault(addonId);
        }

        public AppAddonsManager(string dir) {
            AddonsDirectory = dir;
            List = new BetterObservableCollection<AppAddonInformation>();
            ReloadLocalList();
            // TODO: Directory watching
        }

        public bool IsAddonEnabled(string addonId) {
            if (!_locallyLoaded) {
                ReloadLocalList();
            }
            
            return GetById(addonId)?.IsReady ?? false;
        }

        public string GetAddonDirectory(string addonId) {
            return Path.Combine(AddonsDirectory, addonId);
        }

        public string GetAddonFilename(string addonId, string fileId) {
            return Path.Combine(AddonsDirectory, addonId, fileId);
        }

        public void ReloadLocalList() {
            try {
                List.ReplaceEverythingBy(Directory.GetDirectories(AddonsDirectory)
                        .Select(x => Path.Combine(x, ManifestName))
                        .Where(File.Exists)
                        .Select(x => JsonConvert.DeserializeObject<AppAddonInformation>(File.ReadAllText(x)))
                        .Where(x => x.IsAllRight));
            } catch (Exception e) {
                List.Clear();
                Logging.Warning("cannot get list of installed addons: " + e);
            }

            _locallyLoaded = true;
        }

        public async void UpdateList() {
            if (!_locallyLoaded) {
                ReloadLocalList();
            }

            var list = await DownloadAndParseList();
            if (list == null) {
                Logging.Warning("addons list download failed");
                return; // TODO: informing
            }

            foreach (var addon in list) {
                var local = GetById(addon.Id);
                if (local != null) {
                    List.Remove(local);
                    addon.InstalledVersion = local.InstalledVersion;
                }

                List.Add(addon);
            }
        }

        public async Task<IEnumerable<AppAddonInformation>> DownloadAndParseList() {
            try {
                var loaded = await CmApiProvider.GetStringAsync("addons/list");
                return loaded == null ? null : JsonConvert.DeserializeObject<AppAddonInformation[]>(loaded).Where(x => x.IsAllRight);
            } catch (Exception e) {
                Logging.Warning("cannot download addons list: " + e);
                return null;
            }
        }

        public async Task InstallAppAddon(AppAddonInformation addon) {
            var destination = GetAddonDirectory(addon.Id);

            try {
                addon.IsInstalling = true;
                var data = await CmApiProvider.GetDataAsync($"addons/get/{addon.Id}");
                if (data == null) return;

                await Task.Run(() => {
                    if (Directory.Exists(destination)) {
                        FileUtils.Recycle(destination);
                    }

                    using (var stream = new MemoryStream(data, false))
                    using (var archive = new ZipArchive(stream)) {
                        archive.ExtractToDirectory(destination);
                    }
                });

                addon.InstalledVersion = addon.Version;
                addon.IsEnabled = true;

                File.WriteAllText(Path.Combine(destination, ManifestName), JsonConvert.SerializeObject((object) addon));
            } catch (Exception e) {
                Logging.Warning("cannot install addon: " + e);
                // TODO: something better
            } finally {
                addon.IsInstalling = false;
            }
        }

        public const string ManifestName = "Manifest.json";

        public void RemoveAddon(AppAddonInformation addon) {
            throw new NotImplementedException();
        }

        internal void OnAddonEnabled(AppAddonInformation addon, bool value) {
            (value ? AddonEnabled : AddonDisabled)?.Invoke(this, new AppAddonEventHandlerArgs { AddonId = addon.Id });
        }
    }

    public delegate void AddonEventHandler(object sender, AppAddonEventHandlerArgs args);

    public class AppAddonEventHandlerArgs {
        public string AddonId;
    }
}
