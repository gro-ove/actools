using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Lists;
using AcManager.Tools.SemiGui;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Managers.Plugins {
    public class PluginsManager {
        public static PluginsManager Instance { get; private set; }

        public static PluginsManager Initialize(string dir) {
            if (Instance != null) throw new Exception("Already initialized");
            return Instance = new PluginsManager(dir);
        }

        public readonly string PluginsDirectory;
        public readonly BetterObservableCollection<PluginEntry> List;

        private bool _locallyLoaded;

        public event AddonEventHandler PluginEnabled;
        public event AddonEventHandler PluginDisabled;

        [CanBeNull]
        public PluginEntry GetById(string addonId) {
            return List.GetByIdOrDefault(addonId);
        }

        public PluginsManager(string dir) {
            PluginsDirectory = dir;
            List = new BetterObservableCollection<PluginEntry>();
            ReloadLocalList();
            // TODO: Directory watching
        }

        public bool IsPluginEnabled([Localizable(false)] string id) {
            if (!_locallyLoaded) {
                ReloadLocalList();
            }
            
            return GetById(id)?.IsReady ?? false;
        }

        public string GetPluginDirectory([Localizable(false)] string id) {
            return Path.Combine(PluginsDirectory, id);
        }

        public string GetPluginFilename([Localizable(false)] string id, [Localizable(false)] string fileId) {
            return Path.Combine(PluginsDirectory, id, fileId);
        }

        public void ReloadLocalList() {
            try {
                List.ReplaceEverythingBy(Directory.GetDirectories(PluginsDirectory)
                        .Select(x => Path.Combine(x, ManifestName))
                        .Where(File.Exists)
                        .Select(x => JsonConvert.DeserializeObject<PluginEntry>(File.ReadAllText(x)))
                        .Where(x => x.IsAllRight));
            } catch (Exception e) {
                List.Clear();
                Logging.Warning("Cannot get list of installed plugins: " + e);
            }

            _locallyLoaded = true;
        }

        public async Task UpdateList() {
            if (!_locallyLoaded) {
                ReloadLocalList();
            }

            var list = await DownloadAndParseList();
            if (list == null) {
                Logging.Warning("Plugins list download failed");
                return; // TODO: informing
            }

            foreach (var plugin in list) {
                var local = GetById(plugin.Id);
                if (plugin.IsHidden && !SettingsHolder.Common.DeveloperMode) continue;

                if (local != null) {
                    List.Remove(local);
                    plugin.InstalledVersion = local.InstalledVersion;
                }

                List.Add(plugin);
            }
        }

        public async Task<IEnumerable<PluginEntry>> DownloadAndParseList() {
            try {
                var loaded = await CmApiProvider.GetStringAsync("plugins/list");
                return loaded == null ? null : JsonConvert.DeserializeObject<PluginEntry[]>(loaded).Where(x => x.IsAllRight);
            } catch (Exception e) {
                Logging.Warning("Cannot download plugins list: " + e);
                return null;
            }
        }

        public async Task InstallPlugin(PluginEntry plugin, IProgress<double?> progress = null, CancellationToken cancellation = default(CancellationToken)) {
            var destination = GetPluginDirectory(plugin.Id);

            try {
                plugin.IsInstalling = true;

                var data = await CmApiProvider.GetDataAsync($"plugins/get/{plugin.Id}", progress, cancellation);
                if (data == null || cancellation.IsCancellationRequested) return;

                await Task.Run(() => {
                    if (Directory.Exists(destination)) {
                        FileUtils.Recycle(destination);
                    }

                    using (var stream = new MemoryStream(data, false))
                    using (var archive = new ZipArchive(stream)) {
                        archive.ExtractToDirectory(destination);
                    }
                }, cancellation);
                if (cancellation.IsCancellationRequested) return;

                plugin.InstalledVersion = plugin.Version;
                File.WriteAllText(Path.Combine(destination, ManifestName), JsonConvert.SerializeObject(plugin));

                if (plugin.IsEnabled) {
                    PluginEnabled?.Invoke(this, new AppAddonEventHandlerArgs { PluginId = plugin.Id });
                }
            } catch (Exception e) {
                NonfatalError.Notify(Resources.Plugins_CannotInstall, e);
            } finally {
                plugin.IsInstalling = false;
            }
        }

        public const string ManifestName = "Manifest.json";

        public void RemoveAddon(PluginEntry plugin) {
            throw new NotImplementedException();
        }

        internal void OnPluginEnabled(PluginEntry plugin, bool value) {
            (value ? PluginEnabled : PluginDisabled)?.Invoke(this, new AppAddonEventHandlerArgs { PluginId = plugin.Id });
        }

        public bool HasAnyNew() {
            // TODO
            return false;
        }
    }

    public delegate void AddonEventHandler(object sender, AppAddonEventHandlerArgs args);

    public class AppAddonEventHandlerArgs {
        public string PluginId;
    }
}
