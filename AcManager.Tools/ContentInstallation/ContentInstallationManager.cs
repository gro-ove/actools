using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation {
    public class ContentInstallationManager : NotifyPropertyChanged {
        public static ContentInstallationManager Instance { get; } = new ContentInstallationManager();
        public static IPluginsNavigator PluginsNavigator { get; set; }

        private ContentInstallationManager() {
            DownloadList = new ChangeableObservableCollection<ContentInstallationEntry>();
        }

        public ChangeableObservableCollection<ContentInstallationEntry> DownloadList { get; }

        private bool _isBusyDoingSomething;

        public bool IsBusyDoingSomething {
            get => _isBusyDoingSomething;
            set {
                if (Equals(value, _isBusyDoingSomething)) return;
                _isBusyDoingSomething = value;
                OnPropertyChanged();
            }
        }

        public void UpdateBusyDoingSomething() {
            IsBusyDoingSomething = DownloadList.Aggregate(false,
                    (current, entry) => current | (entry.State == ContentInstallationEntryState.Loading));
        }

        public void Cancel() {
            foreach (var entry in DownloadList.ToList()) {
                entry.CancelCommand.Execute();
            }
        }

        public event EventHandler TaskAdded;

        private readonly TaskCache _taskCache = new TaskCache();

        public Task<bool> InstallAsync([NotNull] string source, ContentInstallationParams installationParams = null) {
            if (source == null) throw new ArgumentNullException(nameof(source));

            return _taskCache.Get(() => ActionExtension.InvokeInMainThread(() => {
                var entry = new ContentInstallationEntry(source, installationParams);
                TaskAdded?.Invoke(this, EventArgs.Empty);
                DownloadList.Add(entry);
                return entry.RunAsync();
            }), source);
        }

        /*
        private void Remove(ContentInstallationEntry entry) {
            entry.Dispose();
            ActionExtension.InvokeInMainThread(() => DownloadList.Remove(entry));
        }

        private async void RemoveLater(ContentInstallationEntry entry) {
            if (entry.Cancelled || entry.Failed == null) {
                if (!entry.UserCancelled) {
                    await Task.Delay(entry.Cancelled ? OptionCancelledDelay : OptionSuccessDelay);
                }

                Remove(entry);
            } else {
                entry.PropertyChanged += (sender, args) => {
                    if (args.PropertyName == nameof(entry.Cancelled)) {
                        Remove(entry);
                    }
                };
            }
        }*/

        public static bool IsRemoteSource(string source) {
            return source.StartsWith(@"http:", StringComparison.OrdinalIgnoreCase) ||
                    source.StartsWith(@"https:", StringComparison.OrdinalIgnoreCase) ||
                    source.StartsWith(@"ftp:", StringComparison.OrdinalIgnoreCase);
        }

        [ItemCanBeNull]
        public static async Task<string> IsRemoteSourceFlexible(string url) {
            // TODO: Fix, change HEAD to GET?
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
                        !filename.EndsWith(@".kn5") && !filename.EndsWith(@".acreplay") && !FileUtils.Affects(AcPaths.GetReplaysDirectory(), filename);
            } catch (Exception) {
                return false;
            }
        }
    }
}