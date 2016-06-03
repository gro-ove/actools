using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Internal;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Lists;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Windows.Controls;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace AcManager.Tools.Miscellaneous {
    public class SharingHelper : NotifyPropertyChanged {
        public static SharingHelper Instance { get; private set; }

        public static void Initialize() {
            Debug.Assert(Instance == null);
            Instance = new SharingHelper();
        }

        public enum EntryType {
            [Description("Controls preset")]
            ControlsPreset,

            [Description("Force Feedback preset")]
            ForceFeedbackPreset,

            [Description("Quick Drive preset")]
            QuickDrivePreset,

            [Description("Replay")]
            Replay,

            [Description("Car setup")]
            CarSetup
        }

        public class SharedEntry : NotifyPropertyChanged {
            public EntryType EntryType { get; set; }

            [CanBeNull]
            public string Name { get; set; }

            [CanBeNull]
            public string Target { get; set; }

            public string Id { get; set; }

            [JsonIgnore]
            public string Url => InternalUtils.ShareResult.GetUrl(Id);

            public string RemovalKey { get; set; }

            [JsonIgnore]
            private string _json;

            internal static SharedEntry Deserialize(string s) {
                var value = JsonConvert.DeserializeObject<SharedEntry>(s);
                value._json = s;
                return value;
            }

            internal string Serialize() {
                return _json ?? JsonConvert.SerializeObject(this, Formatting.None);
            }
        }

        private BetterObservableCollection<SharedEntry> _history;

        public BetterObservableCollection<SharedEntry> History => _history ?? (_history = LoadHistory());

        private const string Key = "SharedHistory";

        private BetterObservableCollection<SharedEntry> LoadHistory() {
            try {
                return new BetterObservableCollection<SharedEntry>(ValuesStorage.GetStringList(Key).Select(SharedEntry.Deserialize));
            } catch (Exception e) {
                Logging.Warning("[SharingHelper] Can't load history: " + e);
                return new BetterObservableCollection<SharedEntry>();
            }
        }

        private void SaveHistory() {
            ValuesStorage.Set(Key, History.Select(x => x.Serialize()));
        }

        private void AddToHistory(EntryType type, string name, string target, InternalUtils.ShareResult result) {
            History.Add(new SharedEntry {
                EntryType = type,
                Id = result.Id,
                RemovalKey = result.RemovalKey,
                Name = name,
                Target = target
            });
            SaveHistory();
        }

        [CanBeNull]
        public static string Share(EntryType type, string name, string target, byte[] data, string customId = null) {
            var authorName = SettingsHolder.Sharing.ShareAnonymously ? null : SettingsHolder.Sharing.SharingName;
            var result = InternalUtils.ShareEntry(type.ToString(), name, target, authorName, data,
                CmApiProvider.UserAgent, customId);
            if (result == null) return null;

            Instance.AddToHistory(type, name, target, result);
            return result.Url;
        }

        [ItemCanBeNull]
        public static async Task<string> ShareAsync(EntryType type, string name, string target, byte[] data, string customId = null,
                CancellationToken cancellation = default(CancellationToken)) {
            var authorName = SettingsHolder.Sharing.ShareAnonymously ? null : SettingsHolder.Sharing.SharingName;
            var result = await InternalUtils.ShareEntryAsync(type.ToString(), name, target, authorName, data,
                    CmApiProvider.UserAgent, customId, cancellation);
            if (result == null || cancellation.IsCancellationRequested) return null;

            Instance.AddToHistory(type, name, target, result);
            return result.Url;
        }
    }
}
