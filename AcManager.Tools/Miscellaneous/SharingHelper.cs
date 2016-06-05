using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Internal;
using AcManager.Tools.Helpers;
using AcManager.Tools.Helpers.Api;
using AcManager.Tools.Lists;
using AcManager.Tools.SemiGui;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
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

        public static string GetExtenstion(EntryType type) {
            switch (type) {
                case EntryType.CarSetup:
                case EntryType.ControlsPreset:
                case EntryType.ForceFeedbackPreset:
                    return ".ini";

                case EntryType.QuickDrivePreset:
                    return ".cmpreset";

                case EntryType.Replay:
                    return ".lnk";

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public class SharedEntry : NotifyPropertyChanged {
            public string Id { get; set; }

            public EntryType EntryType { get; set; }

            [CanBeNull]
            public string Name { get; set; }

            [CanBeNull]
            public string Target { get; set; }

            [JsonIgnore]
            [CanBeNull]
            public string Author { get; set; }

            [JsonIgnore]
            [CanBeNull]
            public byte[] Data { get; set; }

            [JsonIgnore]
            public string Url => InternalUtils.ShareResult.GetUrl(Id);

            [CanBeNull]
            public string RemovalKey { get; set; }

            [JsonIgnore]
            private string _json;

            [NotNull]
            public string GetFileName() {
                return (Name ?? EntryType.GetDescription()) +
                        // (Target == null ? "" : " for " + Target) +
                        (Author == null ? "" : " (" + Author + ")") + 
                        GetExtenstion(EntryType);
            }

            internal static SharedEntry Deserialize(string s) {
                var value = JsonConvert.DeserializeObject<SharedEntry>(s);
                value._json = s;
                return value;
            }

            internal string Serialize() {
                return _json ?? JsonConvert.SerializeObject(this, Formatting.None);
            }
        }

        [ItemCanBeNull]
        public static async Task<SharedEntry> GetSharedAsync(string id, CancellationToken cancellation = default(CancellationToken)) {
            InternalUtils.SharedEntryLoaded loaded;
            try {
                loaded = await InternalUtils.GetSharedEntryAsync(id, CmApiProvider.UserAgent, cancellation);
                if (loaded == null || cancellation.IsCancellationRequested) return null;
            } catch (Exception e) {
                NonfatalError.Notify("Can’t get shared entry", "Make sure Internet connection works.", e);
                return null;
            }

            EntryType entryType;
            try {
                entryType = (EntryType)Enum.Parse(typeof(EntryType), loaded.EntryType);
            } catch (Exception) {
                NonfatalError.Notify($"Can’t get shared entry, its type {loaded.EntryType} is not supported", "Make sure you have the latest version of CM.");
                Logging.Warning("[SHARER] Unsupported entry type: " + loaded.EntryType);
                return null;
            }

            return new SharedEntry {
                Id = id, EntryType = entryType, Name = loaded.Name, Target = loaded.Target, Author = loaded.Author, Data = loaded.Data
            };
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
                EntryType = type, Id = result.Id, RemovalKey = result.RemovalKey, Name = name, Target = target
            });
            SaveHistory();
        }

        [ItemCanBeNull]
        public static async Task<string> ShareAsync(EntryType type, string name, string target, byte[] data, string customId = null, CancellationToken cancellation = default(CancellationToken)) {
            var authorName = SettingsHolder.Sharing.ShareAnonymously ? null : SettingsHolder.Sharing.SharingName;
            var result = await InternalUtils.ShareEntryAsync(type.ToString(), name, target, authorName, data, CmApiProvider.UserAgent, customId, cancellation);
            if (result == null || cancellation.IsCancellationRequested) return null;

            Instance.AddToHistory(type, name, target, result);
            return result.Url;
        }
    }
}
