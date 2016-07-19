using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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
    public enum SharedEntryType {
        [LocalizedDescription("Shared_ControlsPreset")]
        ControlsPreset,

        [LocalizedDescription("Shared_ForceFeedbackPreset")]
        ForceFeedbackPreset,

        [LocalizedDescription("Shared_QuickDrivePreset")]
        QuickDrivePreset,

        [LocalizedDescription("Shared_Replay")]
        Replay,

        [LocalizedDescription("Shared_CarSetup")]
        CarSetup,

        [LocalizedDescription("Shared_PpFilter")]
        PpFilter,

        [LocalizedDescription("Shared_Weather")]
        Weather
    }

    public class SharedEntry : NotifyPropertyChanged {
        public string Id { get; set; }

        public SharedEntryType EntryType { get; set; }

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
            switch (EntryType) {
                case SharedEntryType.Weather:
                    return Regex.Replace(Target ?? Name ?? @"shared_weather", @"\W+", "").ToLowerInvariant();

                default:
                    // TODO: even localized?
                    return (Name ?? EntryType.GetDescription()) +
                            // (Target == null ? "" : " for " + Target) +
                            (Author == null ? "" : @" (" + Author + @")") + SharingHelper.GetExtenstion(EntryType);
            }
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

    public class SharedMetadata : Dictionary<string, string> {}

    public class SharingHelper : NotifyPropertyChanged {
        public static SharingHelper Instance { get; private set; }

        public static void Initialize() {
            Debug.Assert(Instance == null);
            Instance = new SharingHelper();
        }

        public static string GetExtenstion(SharedEntryType type) {
            switch (type) {
                case SharedEntryType.CarSetup:
                case SharedEntryType.ControlsPreset:
                case SharedEntryType.ForceFeedbackPreset:
                case SharedEntryType.PpFilter:
                    return @".ini";

                case SharedEntryType.QuickDrivePreset:
                    return @".cmpreset";

                case SharedEntryType.Replay:
                    return @".lnk";

                case SharedEntryType.Weather:
                    return "";

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private const string IniMetadataPrefix = "# __cmattr:";

        public static string SetMetadata(SharedEntryType type, string data, SharedMetadata metadata) {
            switch (type) {
                case SharedEntryType.CarSetup:
                    var s = new StringBuilder();
                    foreach (var pair in metadata.Where(x => x.Value != null)) {
                        if (pair.Key.Contains(@":")) {
                            throw new Exception(@"Invalid key");
                        }
                        s.Append(IniMetadataPrefix + pair.Key + @":" + ValuesStorage.Encode(pair.Value) + '\n');
                    }
                    return s + data;

                default:
                    throw new NotSupportedException();
            }
        }

        public static SharedMetadata GetMetadata(SharedEntryType type, string data, out string cleaned) {
            switch (type) {
                case SharedEntryType.CarSetup:
                    var r = new SharedMetadata();
                    var s = data.Split('\n');
                    foreach (var k in s.Where(x => x.StartsWith(IniMetadataPrefix)).Select(l => l.Split(new[] { ':' }, 3)).Where(k => k.Length == 3)) {
                        r[k[1]] = ValuesStorage.Decode(k[2]);
                    }
                    cleaned = s.Where(x => !x.StartsWith(IniMetadataPrefix)).JoinToString('\n');
                    return r;

                default:
                    throw new NotSupportedException();
            }
        }

        [ItemCanBeNull]
        public static async Task<SharedEntry> GetSharedAsync(string id, CancellationToken cancellation = default(CancellationToken)) {
            InternalUtils.SharedEntryLoaded loaded;
            try {
                loaded = await InternalUtils.GetSharedEntryAsync(id, CmApiProvider.UserAgent, cancellation);
                if (loaded == null || cancellation.IsCancellationRequested) {
                    return null;
                }
            } catch (Exception e) {
                NonfatalError.Notify(Resources.SharingHelper_CannotGetShared, Resources.SharingHelper_CannotGetShared_Commentary, e);
                return null;
            }

            SharedEntryType entryType;
            try {
                entryType = (SharedEntryType)Enum.Parse(typeof(SharedEntryType), loaded.EntryType);
            } catch (Exception) {
                NonfatalError.Notify(string.Format(Resources.SharingHelper_NotSupported, loaded.EntryType), Resources.SharingHelper_NotSupported_Commentary);
                Logging.Warning("[SharingHelper] Unsupported entry type: " + loaded.EntryType);
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

        public void AddToHistory(SharedEntryType type, string name, string target, InternalUtils.ShareResult result) {
            History.Add(new SharedEntry {
                EntryType = type,
                Id = result.Id,
                RemovalKey = result.RemovalKey,
                Name = name,
                Target = target
            });
            SaveHistory();
        }

        [ItemCanBeNull]
        public static async Task<string> ShareAsync(SharedEntryType type, string name, string target, byte[] data, string customId = null, CancellationToken cancellation = default(CancellationToken)) {
            var authorName = SettingsHolder.Sharing.ShareAnonymously ? null : SettingsHolder.Sharing.SharingName;
            var result = await InternalUtils.ShareEntryAsync(type.ToString(), name, target, authorName, data, CmApiProvider.UserAgent, customId, cancellation);
            if (result == null || cancellation.IsCancellationRequested) {
                return null;
            }

            Instance.AddToHistory(type, name, target, result);
            return result.Url;
        }
    }
}
