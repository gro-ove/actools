using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AcTools.Utils;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation {
    public class ContentInstallationManager : NotifyPropertyChanged {
        public static TimeSpan OptionSuccessDelay = TimeSpan.FromSeconds(3);
        public static TimeSpan OptionCancelledDelay = TimeSpan.FromSeconds(0);
        public static TimeSpan OptionBiggestDelay = TimeSpan.FromSeconds(15);

        public static ContentInstallationManager Instance { get; } = new ContentInstallationManager();
        public static IPluginsNavigator PluginsNavigator { get; set; }

        private ContentInstallationManager() {
            Queue = new ChangeableObservableCollection<ContentInstallationEntry>();
        }

        public ChangeableObservableCollection<ContentInstallationEntry> Queue { get; }

        private bool _busyDoingSomething;

        public bool BusyDoingSomething {
            get => _busyDoingSomething;
            set {
                if (Equals(value, _busyDoingSomething)) return;
                _busyDoingSomething = value;
                OnPropertyChanged();
            }
        }

        public void UpdateBusyDoingSomething() {
            BusyDoingSomething = Queue.Aggregate(false,
                    (current, entry) => current | entry.State == ContentInstallationEntryState.Loading);
        }

        private readonly Dictionary<string, Task<bool>> _tasks = new Dictionary<string, Task<bool>>();

        private async Task<bool> InstallAsyncInternal([NotNull] string source, ContentInstallationParams installationParams) {
            var entry = new ContentInstallationEntry(source, installationParams);
            ActionExtension.InvokeInMainThread(() => Queue.Add(entry));
            var result = await entry.RunAsync();
            _tasks.Remove(source);
            await Task.Delay(1);
            RemoveLater(entry);
            return result;
        }

        public void Cancel() {
            foreach (var entry in Queue) {
                entry.CancelCommand.Execute();
            }
        }

        public event EventHandler TaskAdded;

        public Task<bool> InstallAsync([NotNull] string source, ContentInstallationParams installationParams = null) {
            if (source == null) throw new ArgumentNullException(nameof(source));

            TaskAdded?.Invoke(this, EventArgs.Empty);
            return _tasks.TryGetValue(source, out Task<bool> task) ? task : (_tasks[source] = InstallAsyncInternal(source, installationParams));
        }

        private void Remove(ContentInstallationEntry entry) {
            entry.Dispose();
            ActionExtension.InvokeInMainThread(() => Queue.Remove(entry));
        }

        private async void RemoveLater(ContentInstallationEntry entry) {
            if (entry.Cancelled || entry.Failed == null) {
                await Task.Delay(entry.Cancelled ? OptionCancelledDelay : OptionSuccessDelay);
                Remove(entry);
            } else {
                entry.PropertyChanged += (sender, args) => {
                    if (args.PropertyName == nameof(entry.Cancelled)) {
                        Remove(entry);
                    }
                };
            }
        }

        public static bool IsRemoteSource(string source) {
            return source.StartsWith(@"http:", StringComparison.OrdinalIgnoreCase) ||
                    source.StartsWith(@"https:", StringComparison.OrdinalIgnoreCase) ||
                    source.StartsWith(@"ftp:", StringComparison.OrdinalIgnoreCase);
        }

        [ItemCanBeNull]
        public static async Task<string> IsRemoteSourceFlexible(string url) {
            if (!Regex.IsMatch(url, @"^(?:[\w-]+\.)*[\w-]+\.[\w-]+/.+$")) return null;

            try {
                url = new UriBuilder(url).ToString();
                using (var killer = KillerOrder.Create(WebRequest.Create(url) as HttpWebRequest, TimeSpan.FromSeconds(0.5))) {
                    var request = killer.Victim;
                    request.Method = "HEAD";
                    using (var response = await request.GetResponseAsync()) {
                        return (response as HttpWebResponse)?.StatusCode == HttpStatusCode.OK ? url.Replace(@":80/", @"/") : null;
                    }
                }
            } catch (Exception) {
                return null;
            }
        }

        public static bool IsAdditionalContent(string filename) {
            // TODO: or PP-filter, or …?
            try {
                return FileUtils.IsDirectory(filename) ||
                        !filename.EndsWith(@".kn5") && !filename.EndsWith(@".acreplay") && !FileUtils.IsAffected(FileUtils.GetReplaysDirectory(), filename);
            } catch (Exception) {
                return false;
            }
        }
    }
}