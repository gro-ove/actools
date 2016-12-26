using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Helpers.Api.Kunos;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Converters;
using JetBrains.Annotations;

namespace AcManager.Tools.Managers.Online {
    public class Holder<T> : IDisposable {
        private readonly Action<T> _dispose;

        public T Value { get; }

        public Holder(T value, Action<T> dispose) {
            _dispose = dispose;
            Value = value;
        }

        public void Dispose() {
            _dispose.Invoke(Value);
        }

        public static Holder<T> CreateNonHolding(T value) {
            return new Holder<T>(value, v => { });
        }
    }

    public class ReleasedEventArgs<T> : EventArgs {
        public ReleasedEventArgs(T value) {
            Value = value;
        }

        public T Value { get; }
    }

    public class HoldedList<T> {
        private readonly List<T> _holded;

        public HoldedList(int capacity) {
            _holded = new List<T>(capacity);
        }

        [CanBeNull, ContractAnnotation(@"value: null => null")]
        public Holder<T> Get([CanBeNull] T value) {
            if (ReferenceEquals(value, null)) return null;

            var holder = new Holder<T>(value, Release);
            _holded.Add(value);
            return holder;
        }

        public event EventHandler<ReleasedEventArgs<T>> Released; 

        private void Release(T obj) {
            for (var i = 0; i < _holded.Count; i++) {
                if (ReferenceEquals(obj, _holded[i])) {
                    _holded.RemoveAt(i);
                    Released?.Invoke(this, new ReleasedEventArgs<T>(obj));
                    return;
                }
            }
        }

        public bool Contains(T obj) {
            for (var i = 0; i < _holded.Count; i++) {
                if (ReferenceEquals(obj, _holded[i])) {
                    return true;
                }
            }

            return false;
        }
    }

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