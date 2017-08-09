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
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Managers.Plugins {
    public class PluginsManager : NotifyPropertyChanged {
        public static PluginsManager Instance { get; private set; }

        public static PluginsManager Initialize(string dir) {
            if (Instance != null) throw new Exception("Already initialized");
            return Instance = new PluginsManager(dir);
        }

        public readonly string PluginsDirectory;

        public BetterObservableCollection<PluginEntry> List { get; }
        public BetterListCollectionView ListView { get; }
        public BetterListCollectionView RecommendedListView { get; }

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
            ListView = new BetterListCollectionView(List);
            ListView.SortDescriptions.Add(new SortDescription(nameof(PluginEntry.Name), ListSortDirection.Ascending));
            RecommendedListView = new BetterListCollectionView(List);
            RecommendedListView.SortDescriptions.Add(new SortDescription(nameof(PluginEntry.Name), ListSortDirection.Ascending));
            RecommendedListView.Filter = o => (o as PluginEntry)?.IsRecommended == true;
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

        private class UniqueIds : IEqualityComparer<PluginEntry> {
            public bool Equals(PluginEntry x, PluginEntry y) {
                return x?.Id == y?.Id;
            }

            public int GetHashCode(PluginEntry obj) {
                return obj?.Id?.GetHashCode() ?? 0;
            }
        }

        public void ReloadLocalList() {
            try {
                List.ReplaceEverythingBy(Directory.GetDirectories(PluginsDirectory)
                                                  .Select(x => Path.Combine(x, ManifestName))
                                                  .Where(File.Exists)
                                                  .Select(x => JsonConvert.DeserializeObject<PluginEntry>(File.ReadAllText(x)))
                                                  .Where(x => x.IsAllRight)
                                                  .Distinct(new UniqueIds())
                                                  .ToList());
            } catch (Exception e) {
                List.Clear();
                Logging.Warning("Cannot get list of installed plugins: " + e);
            }

            _locallyLoaded = true;
        }

        private static DateTime _lastUpdated;

        public Task UpdateIfObsolete() {
            if (DateTime.Now - _lastUpdated < TimeSpan.FromMinutes(10d)) {
                return Task.Delay(0);
            }

            _lastUpdated = DateTime.Now;
            return Instance.UpdateList();
        }

        private async Task UpdateList() {
            if (!_locallyLoaded) {
                ReloadLocalList();
            }

            var list = await DownloadAndParseList();
            if (list == null) return;

            foreach (var plugin in list) {
                if (plugin.IsObsolete || plugin.IsHidden && !SettingsHolder.Common.DeveloperMode) continue;

                var local = GetById(plugin.Id);
                if (local != null) {
                    List.Remove(local);
                    plugin.InstalledVersion = local.InstalledVersion;
                }

                List.Add(plugin);
            }
        }

        private static async Task<IEnumerable<PluginEntry>> DownloadAndParseList() {
            try {
                var loaded = await CmApiProvider.GetStringAsync("plugins/list");
                return loaded == null ? null : JsonConvert.DeserializeObject<PluginEntry[]>(loaded).Where(x => x.IsAllRight);
            } catch (Exception e) {
                NonfatalError.NotifyBackground("Can’t download plugins list", e);
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
                NonfatalError.Notify(ToolsStrings.Plugins_CannotInstall, e);
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
