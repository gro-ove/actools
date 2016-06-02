using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using AcManager.Tools.AcManagersNew;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Helpers.Api.Kunos;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Online {
    public class AcServerEntryWrapper : AcItemWrapper {
        public string Group { get; }

        public AcServerEntryWrapper([NotNull] IAcWrapperLoader loader, [NotNull] AcPlaceholderNew initialValue)
                : base(loader, initialValue) {}
    }

    public class RecentManager : BaseOnlineManager {
        public static int OptionScanPingTimeout = 200;

        public static void Initialize() {
            Debug.Assert(Instance == null);
            Instance = new RecentManager();
        }

        public static RecentManager Instance { get; private set; }

        private const string KeySavedServers = "RecentManager.SavedServers";
        
        public async Task AddServer([NotNull] string address, IProgress<string> progress = null) {
            if (address == null) throw new ArgumentNullException(nameof(address));

            // assume address is something like [HOSTNAME]:[HTTP PORT]
            string ip;
            int port;
            if (!KunosApiProvider.ParseAddress(address, out ip, out port)) {
                throw new Exception("Can’t parse address");
            }

            if (port > 0) {
                progress?.Report("Getting direct information…");
                var information = await Task.Run(() => KunosApiProvider.TryToGetInformationDirect(ip, port));

                // assume address is [HOSTNAME]:[TCP PORT]
                if (information == null) {
                    progress?.Report("Trying to ping to find out HTTP port…");
                    var httpPort = 0;
                    if (await Task.Run(() => KunosApiProvider.TryToPingServer(ip, port, out httpPort)) != null) {
                        progress?.Report("Getting direct information (second attempt)…");
                        information = await Task.Run(() => KunosApiProvider.TryToGetInformationDirect(ip, httpPort));
                    }
                }

                if (information == null) {
                    throw new Exception("Can’t access server");
                }

                AddToSavedList(information.Ip, information.PortC);
                CreateAndAddEntry(information);
            } else {
                // assume address is [HOSTNAME]
                progress?.Report("Scanning…");

                var scanned = 0;
                var found = 0;
                var total = SettingsHolder.Online.PortsEnumeration.ToPortsDiapason().Count();

                await TaskExtension.WhenAll(
                        SettingsHolder.Online.PortsEnumeration.ToPortsDiapason().Select(async p => {
                            var httpPort = 0;
                            if (await Task.Run(() => KunosApiProvider.TryToPingServer(ip, p, OptionScanPingTimeout, out httpPort)) != null && httpPort > 1024 &&
                                    httpPort < 65536) {
                                var information = await Task.Run(() => KunosApiProvider.TryToGetInformationDirect(ip, httpPort));
                                if (information != null) {
                                    AddToSavedList(ip, httpPort);
                                    found++;

                                    try {
                                        CreateAndAddEntry(information);
                                    } catch (Exception e) {
                                        Logging.Warning("[RECENTMANAGER] Scan add error: " + e);
                                    }
                                }
                            }

                            scanned++;
                            if (progress == null) return;
                            progress.Report($"Scanning ({scanned}/{total}), {found} {PluralizingConverter.Pluralize(found, "server")} found…");
                        }), OptionConcurrentThreadsNumber);

                if (found == 0) {
                    throw new Exception("Nothing found");
                }
            }
        }

        public void AddRecentServer(ServerInformation information) {
            if (LoadedOnly.Any(x => x.Id == information.GetUniqueId())) return;

            try {
                CreateAndAddEntry(information);
            } catch (Exception e) {
                Logging.Warning("[RECENTMANAGER] Recent add error: " + e);
            }

            AddToRecentList(information.Ip, information.PortC);
        }

        private IEnumerable<string> FilterList(IEnumerable<string> list) {
            return list.Select(x => x.Trim()).Where(x => x.Length > 0 && !x.StartsWith("#"));
        }

        private IEnumerable<string> LoadAndFilterList(string filename) {
            return File.Exists(filename) ? FilterList(File.ReadAllLines(filename)) : new string[0];
        }

        private string MainListFilename => FilesStorage.Instance.GetFilename("Online Servers", "Main List.txt");

        private string RecentListFilename => FilesStorage.Instance.GetFilename("Online Servers", "Recent.txt");

        public IEnumerable<string> LoadList() {
            return LoadAndFilterList(MainListFilename).Union(LoadAndFilterList(RecentListFilename));
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void AddToSavedList(string ip, int httpPort) {
            // TODO: watcher
            using (var writer = new StreamWriter(MainListFilename, true)) {
                writer.WriteLine($"{ip}:{httpPort}");
            }
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void AddToRecentList(string ip, int httpPort) {
            var filename = RecentListFilename;
            File.WriteAllLines(filename, LoadAndFilterList(filename).Append($"{ip}:{httpPort}").TakeLast(25));
        }

        protected override IEnumerable<AcPlaceholderNew> ScanInner() {
            return from address in ValuesStorage.GetStringList(KeySavedServers)
                   let entry = ServerEntry.FromAddress(this, address)
                   where entry != null
                   select entry;
        }

        protected override async Task<IEnumerable<AcPlaceholderNew>> ScanInnerAsync() {
            ScanDefferedAsync().Forget();
            await Task.Delay(300);
            return new AcPlaceholderNew[0];
        }

        private async Task ScanDefferedAsync() {
            BackgroundLoading = true;
            Pinged = 0;

            UnavailableList.Clear();
            await TaskExtension.WhenAll(LoadList().Select(async address => {
                try {
                    var entry = await Task.Run(() => ServerEntry.FromAddress(this, address));
                    if (entry == null) {
                        UnavailableList.Add(address);
                        return;
                    }

                    InnerWrappersList.Add(new AcItemWrapper(this, entry));

                    if (entry.Status == ServerStatus.Unloaded) {
                        await entry.Update(ServerEntry.UpdateMode.Lite);
                    }

                    Pinged++;
                } catch (Exception e) {
                    Logging.Warning("[LANMANAGER] Cannot create ServerEntry: " + e);
                }
            }), OptionConcurrentThreadsNumber);

            BackgroundLoading = false;
        }
    }
}
