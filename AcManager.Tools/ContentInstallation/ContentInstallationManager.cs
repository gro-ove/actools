using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Media;
using AcManager.Tools.Managers;
using AcTools.Utils;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Tools.ContentInstallation {
    public class ContentInstallationManager : NotifyPropertyChanged {
        public static ContentInstallationManager Instance { get; } = new ContentInstallationManager();

        public static IPluginsSusanin PluginsSusanin { get; set; }

        private ContentInstallationManager() {
            Queue = new BetterObservableCollection<ContentInstallationEntry>();
        }

        public BetterObservableCollection<ContentInstallationEntry> Queue { get; }

        private readonly Dictionary<string, Task<bool>> _tasks = new Dictionary<string, Task<bool>>();

        private async Task<bool> InstallAsyncInternal([NotNull] string source, ContentInstallationParams installationParams) {
            var entry = new ContentInstallationEntry(source, installationParams);
            ActionExtension.InvokeInMainThread(() => Queue.Add(entry));
            var result = await entry.RunAsync();
            await Task.Delay(1);
            RemoveLater(entry);
            _tasks.Remove(source);
            return result;
        }

        public Task<bool> InstallAsync([NotNull] string source, ContentInstallationParams installationParams = null) {
            return _tasks.TryGetValue(source, out Task<bool> task) ? task : (_tasks[source] = InstallAsyncInternal(source, installationParams));
        }

        private async void RemoveLater(ContentInstallationEntry entry) {
            await Task.Delay(TimeSpan.FromSeconds(entry.Failed != null ? 7d : 3d));
            ActionExtension.InvokeInMainThread(() => Queue.Remove(entry));
        }

        public static bool IsRemoteSource(string source) {
            return source.StartsWith(@"http:", StringComparison.OrdinalIgnoreCase) ||
                    source.StartsWith(@"https:", StringComparison.OrdinalIgnoreCase) ||
                    source.StartsWith(@"ftp:", StringComparison.OrdinalIgnoreCase);
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

    public class ContentInstallationParams {
        public static readonly ContentInstallationParams Default = new ContentInstallationParams();

        public string CarId { get; set; }
    }

    public enum ContentInstallationEntryState {
        Loading, PasswordRequired, WaitingForConfirmation, Finished
    }

    public interface IPluginsSusanin {
        void ShowPluginsList();
    }
}