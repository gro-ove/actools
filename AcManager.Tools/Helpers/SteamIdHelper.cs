using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AcManager.Internal;
using AcManager.Tools.Helpers.Api;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;
using Microsoft.Win32;

namespace AcManager.Tools.Helpers {
    public class SteamIdHelperEventArgs : EventArgs {
        public readonly string PreviousValue, NewValue;

        internal SteamIdHelperEventArgs(string previousValue, string newValue) {
            PreviousValue = previousValue;
            NewValue = newValue;
        }
    }

    public class SteamIdHelper : NotifyPropertyChanged {
        public static string OptionForceValue = null;

        private const string Key = "SteamId";
        private const string NoneValue = "-";

        public static SteamIdHelper Instance { get; private set; }

        public static void Initialize(string forced = null) {
            if (Instance != null) throw new Exception(@"Already initialized");
            Instance = new SteamIdHelper(forced);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void SetSteamIdInner_Impl(string value) {
            InternalUtils.SetSteamId(value);
        }

        private static void SetSteamIdInner(string value) {
            try {
                SetSteamIdInner_Impl(value);
            } catch {
                // ignore
            }
        }

        private SteamIdHelper(string forced) {
            if (IsValidSteamId(forced)) {
                _value = forced;
                _loaded = true;
                SetSteamIdInner(_value);
            } else {
                if (forced != null) {
                    Logging.Warning($"Invalid forced value: “{forced}”");
                } else {
                    if (ValuesStorage.Contains(Key)) {
                        var loaded = ValuesStorage.Get<string>(Key);
                        _value = loaded == NoneValue ? null : loaded;
                        _loaded = true;
                        SetSteamIdInner(_value);
                    }
                }
            }
        }

        private string _value;
        private bool _loaded, _default;

        private static string GetDefaultValue() {
            return TryToFind().FirstOrDefault()?.SteamId;
        }

        [CanBeNull]
        public string Value {
            get {
                if (!_loaded && !_default) {
                    _value = GetDefaultValue();
                    _default = true;
                    SetSteamIdInner(_value);
                }

                return _value;
            }
            set {
                if (_loaded && Equals(_value, value)) return;

                var oldValue = _value;
                if (!Equals(oldValue, value)) {
                    _value = value;
                    OnPropertyChanged();
                }

                _loaded = true;
                ValuesStorage.Set(Key, _value ?? NoneValue);
                SetSteamIdInner(_value);
                SettingsHolder.Online.CachingServerAvailable = false;
            }
        }

        public bool IsReady => Value != null;

        public static IEnumerable<SteamProfile> TryToFind() {
            // TODO: if (OptionForceValue != null) return OptionForceValue;

            Vdf parsed;
            try {
                var regKey = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                if (regKey == null) yield break;

                var steamPath = regKey.GetValue("SteamPath").ToString();
                var config = File.ReadAllText(Path.Combine(steamPath, @"config", @"loginusers.vdf"));

                parsed = Vdf.Parse(config).Children.GetValueOrDefault("users");
                if (parsed == null) {
                    throw new Exception("Config is invalid");
                }
            } catch (Exception e) {
                NonfatalError.Notify("Can’t get Steam ID from its config", e);
                yield break;
            }

            string selectedId = null;
            try {
                var selectedKey = new IniFile(AcPaths.GetRaceIniFilename());
                selectedId = selectedKey["REMOTE"].GetNonEmpty("GUID");
            } catch (Exception) {
                // ignored
            }

            if (selectedId != null && parsed.Children.TryGetValue(selectedId, out var selectedSection)) {
                yield return new SteamProfile(selectedId, selectedSection.Values.GetValueOrDefault("PersonaName"));
            }

            foreach (var pair in parsed.Children.Where(x => x.Key != selectedId)) {
                yield return new SteamProfile(pair.Key, pair.Value.Values.GetValueOrDefault("PersonaName"));
            }
        }

        public static bool IsValidSteamId(string id) {
            return id != null && Regex.IsMatch(id.Trim(), @"\d{10,30}");
        }

        private static Dictionary<string, string> _steamNameCache = new Dictionary<string, string>();

        [ItemCanBeNull]
        public static async Task<string> GetSteamNameAsync([CanBeNull] string id) {
            if (id == null) return null;
            if (_steamNameCache.TryGetValue(id, out var name)) return name;
            return _steamNameCache[id] = IsValidSteamId(id) ? await Task.Run(() => SteamWebProvider.TryToGetUserName(id)) : null;
        }
    }

    public class SteamProfile : Displayable, IWithId {
        public static readonly SteamProfile None = new SteamProfile();

        private SteamProfile() {
            SteamId = null;
            _profileName = ToolsStrings.Common_None;
        }

        public SteamProfile([NotNull] string steamId, string profileName = null) {
            SteamId = steamId;
            _profileName = profileName;
        }

        private string _profileName;

        [CanBeNull]
        public string ProfileName {
            get => _profileName;
            set {
                if (Equals(value, _profileName)) return;
                _profileName = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(DisplayName));
            }
        }

        public override string DisplayName {
            get => ProfileName == null ? SteamId : SteamId == null ? ProfileName : $"{ProfileName} ({SteamId})";
            set { }
        }

        [CanBeNull]
        public string SteamId { get; }

        string IWithId<string>.Id => SteamId;
    }
}