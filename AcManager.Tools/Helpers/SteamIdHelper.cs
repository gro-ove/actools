using System;
using System.IO;
using System.Security;
using System.Text.RegularExpressions;
using FirstFloor.ModernUI.Helpers;
using Microsoft.Win32;

namespace AcManager.Tools.Helpers {
    public class SteamIdHelperEventArgs : EventArgs {
        public readonly string PreviousValue, NewValue;

        internal SteamIdHelperEventArgs(string previousValue, string newValue) {
            PreviousValue = previousValue;
            NewValue = newValue;
        }
    }

    public class SteamIdHelper {
        public static string OptionForceValue = null;

        public const string Key = "_steam_id";

        public static SteamIdHelper Instance { get; private set; }

        public static SteamIdHelper Initialize() {
            if (Instance != null) throw new Exception("already initialized");
            return Instance = new SteamIdHelper();
        }

        private SteamIdHelper() {
            Value = ValuesStorage.GetString(Key);
        }

        private string _value;
        private bool _tried;

        public string Value {
            get {
                if (_value != null || _tried) return _value;

                _value = TryToFind();
                _tried = true;
                return _value;
            }
            set {
                if (_value == value) return;

                var oldValue = _value;
                _value = value;
                ValuesStorage.Set(Key, _value);
                
                Changed?.Invoke(this, new SteamIdHelperEventArgs(oldValue, _value));
            }
        }

        public bool IsReady => _value != null;

        public delegate void SteamIdHelperEventHandler(object sender, SteamIdHelperEventArgs e);
        public event SteamIdHelperEventHandler Changed;

        private static string TryToFind() {
            if (OptionForceValue != null) return OptionForceValue;

            Logging.Write("trying to find steam id from steam");
            try {
                var regKey = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
                if (regKey == null) return null;

                var steamPath = regKey.GetValue("SteamPath").ToString();
                var config = File.ReadAllText(Path.Combine(steamPath, "config", "config.vdf"));

                var match = Regex.Match(config, @"""SteamID""\s+""(\d+)""");
                return match.Success ? match.Groups[1].Value : null;
            } catch (SecurityException exception) {
                Logging.Write("- error: {0}", exception);
                return null;
            }
        }
    }
}
