using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
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

        private static RecentManager _instance;

        public static RecentManager Instance => _instance ?? (_instance = new RecentManager());

        private const string KeySavedServers = "RecentManager.SavedServers";
        
        public async Task AddServer([NotNull] string address, IProgress<string> progress = null, CancellationToken cancellation = default(CancellationToken)) {
            if (address == null) throw new ArgumentNullException(nameof(address));

            // assume address is something like [HOSTNAME]:[HTTP PORT]
            string ip;
            int port;
            if (!KunosApiProvider.ParseAddress(address, out ip, out port)) {
                throw new Exception(ToolsStrings.Online_CannotParseAddress);
            }

            if (port > 0) {
                progress?.Report(ToolsStrings.Online_GettingInformationDirectly);
                var information = await KunosApiProvider.TryToGetInformationDirectAsync(ip, port);
                if (cancellation.IsCancellationRequested) return;

                // assume address is [HOSTNAME]:[TCP PORT]
                if (information == null) {
                    progress?.Report(ToolsStrings.Online_TryingToFindOutHttpPort);
                    var pair = await KunosApiProvider.TryToPingServerAsync(ip, port, SettingsHolder.Online.PingTimeout);
                    if (cancellation.IsCancellationRequested) return;

                    if (pair != null) {
                        progress?.Report(ToolsStrings.Online_GettingInformationDirectly_SecondAttempt);
                        information = await KunosApiProvider.TryToGetInformationDirectAsync(ip, pair.Item1);
                        if (cancellation.IsCancellationRequested) return;
                    }
                }

                if (information == null) {
                    throw new Exception(ToolsStrings.Online_CannotAccessServer);
                }

                AddToSavedList(information.Ip, information.PortC);
                CreateAndAddEntry(information);
            } else {
                // assume address is [HOSTNAME]
                progress?.Report(ToolsStrings.Common_Scanning);

                var scanned = 0;
                var found = 0;
                var total = SettingsHolder.Online.PortsEnumeration.ToPortsDiapason().Count();

                await SettingsHolder.Online.PortsEnumeration.ToPortsDiapason().Select(async p => {
                    var pair = await KunosApiProvider.TryToPingServerAsync(ip, p, SettingsHolder.Online.ScanPingTimeout);
                    if (pair != null && pair.Item1 > 1024 && pair.Item1 < 65536) {
                        if (cancellation.IsCancellationRequested) return;

                        var information = await KunosApiProvider.TryToGetInformationDirectAsync(ip, pair.Item1);
                        if (cancellation.IsCancellationRequested) return;

                        if (information != null) {
                            AddToSavedList(ip, pair.Item1);
                            found++;

                            try {
                                CreateAndAddEntry(information);
                            } catch (Exception e) {
                                if (e.Message != ToolsStrings.Common_IdIsTaken) {
                                    Logging.Warning("Scan add error: " + e);
                                }
                            }
                        }
                    }

                    scanned++;
                    if (progress == null) return;
                    progress.Report(string.Format(ToolsStrings.Online_ScanningProgress, scanned, total,
                            PluralizingConverter.PluralizeExt(found, ToolsStrings.Online_ScanningProgress_Found)));
                }).WhenAll(200, cancellation);

                if (found == 0) {
                    throw new InformativeException(ToolsStrings.Online_ScanningNothingFound, ToolsStrings.Online_ScanningNothingFound_Commentary);
                }
            }
        }

        public void AddRecentServer(ServerInformation information) {
            if (LoadedOnly.Any(x => x.Id == information.GetUniqueId())) return;

            try {
                CreateAndAddEntry(information);
            } catch (Exception e) {
                Logging.Warning("Recent add error: " + e);
            }

            AddToRecentList(information.Ip, information.PortC);
        }

        private IEnumerable<string> FilterList(IEnumerable<string> list) {
            return list.Select(x => x.Trim()).Where(x => x.Length > 0 && !x.StartsWith(@"#"));
        }

        private IEnumerable<string> LoadAndFilterList(string filename) {
            return File.Exists(filename) ? FilterList(File.ReadAllLines(filename)) : new string[0];
        }

        private string MainListFilename => FilesStorage.Instance.GetFilename("Online Servers", "Main List.txt");

        private string RecentListFilename => FilesStorage.Instance.GetFilename("Online Servers", "Recent.txt");

        public IEnumerable<string> LoadList() {
            return LoadAndFilterList(MainListFilename).Union(LoadAndFilterList(RecentListFilename));
        }

        private readonly object _saved = new object();
        
        private void AddToSavedList(string ip, int httpPort) {
            lock (_saved) {
                using (var writer = new StreamWriter(MainListFilename, true)) {
                    writer.WriteLine($"{ip}:{httpPort}");
                }
            }
        }

        private readonly object _recent = new object();

        private void AddToRecentList(string ip, int httpPort) {
            lock (_recent) {
                var filename = RecentListFilename;
                File.WriteAllLines(filename, LoadAndFilterList(filename).Append($"{ip}:{httpPort}").TakeLast(25));
            }
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

            try {
                UnavailableList.Clear();
                await LoadList().Select(async address => {
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
                        Logging.Warning("Cannot create ServerEntry: " + e);
                    }
                }).WhenAll(SettingsHolder.Online.PingConcurrency);
            } finally {
                BackgroundLoading = false;
            }
        }
    }
}
