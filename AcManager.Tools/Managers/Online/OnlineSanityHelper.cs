using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using AcManager.Tools.Data;
using AcManager.Tools.Helpers;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Helpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcManager.Tools.Managers.Online {
    public class OnlineSanityHelper {
        private static Regex _sortingNameCleanUp;
        private static Regex _holdingSpot;
        private static Regex _holdingSpotEmptySkin;

        public static void Initialize() {
            ReloadRules();
            DataUpdater.Instance.Updated += (sender, args) => ReloadRules();
        }

        private static void ReloadRules() {
            var file = FilesStorage.Instance.GetContentFile(ContentCategory.Miscellaneous, "OnlineSanityHelper.json");
            try {
                if (file.Exists) {
                    var jObj = JsonConvert.DeserializeObject<JObject>(File.ReadAllText(file.Filename));
                    void LoadRegex(string key, out Regex dst) {
                        var str = jObj.GetStringValueOnly(key);
                        dst = str == null ? null : new Regex(str, RegexOptions.Compiled);
                    }

                    LoadRegex("sortingNameCleanUp", out _sortingNameCleanUp);
                    LoadRegex("holdingSpot", out _holdingSpot);
                    LoadRegex("holdingSpotEmptySkin", out _holdingSpotEmptySkin);
                }
            } catch (Exception e) {
                Logging.Error(e);
            }
        }

        private static bool IsLetterOrExtended(char c) {
            return char.IsLetter(c) || c >= '\u007F';
        }
        
        private static bool IsSortPaddingToken(string token, int from, int to) {
            var len = to - from;
            if (len == 1) return true;

            var hasLetters = false;
            var hasGoodLetters = false;
            var leadingDigits = 0;
            for (int i = from; i < to; ++i) {
                var c = token[i];
                if (IsLetterOrExtended(c)) hasLetters = true;
                else if (!hasLetters) ++leadingDigits;
                if (c >= 'h' && c <= 'z' || c >= 'H' && c <= 'Z') hasGoodLetters = true;
            }

            if (hasLetters && leadingDigits > 2) return true;

            var s = char.ToLower(token[from]);
            if (s >= '0' && s <= (hasLetters ? '1' : '5')) return true;
            if (hasGoodLetters) return len < 3 && s == 'a';
            return s >= 'a' && s <= 'e';
        }
        
        private static StringBuilder _sortingNameSb;

        public static string GetSortingName(string name) {
            if (_sortingNameCleanUp != null) {
                name = _sortingNameCleanUp.Replace(name, "");
            }

            var sb = Interlocked.Exchange(ref _sortingNameSb, null);
            if (sb == null) sb = new StringBuilder();
            sb.Clear();
            
            int b = 0;
            for (int i = 0; i < name.Length + 1; ++i) {
                if (i == name.Length || !IsPartOfWord(name[i])) {
                    if (i > b && !IsSortPaddingToken(name, b, i)) {
                        // once we found a non-padding token, we can just proceed with the rest of the string
                        sb.Append(name.Substring(b));
                        break;
                    }
                    b = i + 1;
                }
            }
            
            if (sb.Length == 0) {
                return @"zz:" + name;
            }
            
            var ret = sb.ToString();
            _sortingNameSb = sb;
            return ret;

            bool IsPartOfWord(char c) {
                return char.IsDigit(c) || IsLetterOrExtended(c);
            }
        }

        public static bool IsHoldingSlot(string name, string skinId) {
            if (string.IsNullOrEmpty(skinId) && _holdingSpotEmptySkin?.IsMatch(name) == true) return true;
            if (_holdingSpot?.IsMatch(name) == true) return true;
            return false;
        }
    }
}