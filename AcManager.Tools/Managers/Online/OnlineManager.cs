using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Helpers.Api.Kunos;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Online {
    public partial class OnlineManager : NotifyPropertyChanged {
        private static OnlineManager _instance;

        public static OnlineManager Instance => _instance ?? (_instance = new OnlineManager());

        public ChangeableObservableCollection<ServerEntry> List { get; } = new ChangeableObservableCollection<ServerEntry>();

        private readonly HoldedList<ServerEntry> _holdedList = new HoldedList<ServerEntry>(4);
        private readonly List<ServerEntry> _removeWhenReleased = new List<ServerEntry>(2);

        private OnlineManager() {
            _holdedList.Released += HoldedList_Released;
        }

        private void HoldedList_Released(object sender, ReleasedEventArgs<ServerEntry> e) {
            var index = _removeWhenReleased.IndexOf(e.Value);
            if (index != -1) { 
                _removeWhenReleased.RemoveAt(index);
                List.Remove(e.Value);
            }
        }

        [CanBeNull]
        public ServerEntry GetById(string id) {
            return List.GetByIdOrDefault(id);
        }

        [CanBeNull]
        public Holder<ServerEntry> HoldById(string id) {
            return _holdedList.Get(GetById(id));
        }

        public bool IsHolded(ServerEntry entry) {
            return _holdedList.Contains(entry);
        }

        public void RemoveWhenReleased(ServerEntry entry) {
            _removeWhenReleased.Add(entry);
        }

        public void AvoidRemoval(ServerEntry entry) {
            _removeWhenReleased.Remove(entry);
        }

        /// <summary>
        /// Throws an exception.
        /// </summary>
        /// <returns>Null if the request was cancelled.</returns>
        [ItemCanBeNull]
        public async Task<IReadOnlyList<ServerInformationComplete>> ScanForServers(string address, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            if (address == null) throw new ArgumentNullException(nameof(address));

            // assume address is something like [HOSTNAME]:[HTTP PORT]
            string ip;
            int port;
            if (!KunosApiProvider.ParseAddress(address, out ip, out port)) {
                throw new Exception(ToolsStrings.Online_CannotParseAddress);
            }

            if (port > 0) {
                progress?.Report(AsyncProgressEntry.FromStringIndetermitate(ToolsStrings.Online_GettingInformationDirectly));

                ServerInformationComplete information;

                try {
                    information = await KunosApiProvider.GetInformationDirectAsync(ip, port);
                } catch (WebException) {
                    if (cancellation.IsCancellationRequested) return null;

                    // assume address is [HOSTNAME]:[TCP PORT]
                    progress?.Report(AsyncProgressEntry.FromStringIndetermitate(ToolsStrings.Online_TryingToFindOutHttpPort));
                    var pair = await KunosApiProvider.TryToPingServerAsync(ip, port, SettingsHolder.Online.PingTimeout);
                    if (cancellation.IsCancellationRequested) return null;

                    if (pair != null) {
                        progress?.Report(AsyncProgressEntry.FromStringIndetermitate(ToolsStrings.Online_GettingInformationDirectly_SecondAttempt));

                        try {
                            information = await KunosApiProvider.GetInformationDirectAsync(ip, pair.Item1);
                        } catch (WebException) {
                            information = null;
                        }
                    } else {
                        information = null;
                    }
                }

                if (cancellation.IsCancellationRequested) return null;
                return information == null ? new ServerInformationComplete[0] : new [] { information };
            } else {
                var result = new List<ServerInformationComplete>();

                // assume address is [HOSTNAME]
                progress?.Report(AsyncProgressEntry.FromStringIndetermitate(ToolsStrings.Common_Scanning));

                var scanned = 0;
                var total = SettingsHolder.Online.PortsEnumeration.ToPortsDiapason().Count();

                await SettingsHolder.Online.PortsEnumeration.ToPortsDiapason().Select(async p => {
                    var pair = await KunosApiProvider.TryToPingServerAsync(ip, p, SettingsHolder.Online.ScanPingTimeout);
                    if (pair != null && pair.Item1 > 1024 && pair.Item1 < 65536) {
                        if (cancellation.IsCancellationRequested) return;

                        try {
                            var information = await KunosApiProvider.GetInformationDirectAsync(ip, pair.Item1);
                            if (cancellation.IsCancellationRequested) return;
                            result.Add(information);
                        } catch (WebException) { }
                    }

                    scanned++;
                    progress?.Report(new AsyncProgressEntry(string.Format(ToolsStrings.Online_ScanningProgress, scanned, total,
                            PluralizingConverter.PluralizeExt(result.Count, ToolsStrings.Online_ScanningProgress_Found)), scanned, total));
                }).WhenAll(200, cancellation);

                return result;
            }
        }
    }
}