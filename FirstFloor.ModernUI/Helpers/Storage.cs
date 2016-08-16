using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    [Localizable(false)]
    public class Storage : NotifyPropertyChanged {
        private readonly Dictionary<string, string> _storage;
        private readonly string _filename;
        private readonly string _encryptionKey;
        private readonly bool _disableCompression;
        private readonly bool _useDeflate;

        public Storage(string filename = null, string encryptionKey = null, bool disableCompression = false, bool useDeflate = false) {
            _storage = new Dictionary<string, string>();
            _filename = filename;
            _encryptionKey = encryptionKey;
            _disableCompression = disableCompression;
            _useDeflate = useDeflate;

            Load();

            _exitEvent += (sender, args) => {
                if (_dirty) {
                    Save();
                }
            };
        }

        public int Count => _storage.Count;

        private TimeSpan? _previosSaveTime;

        public TimeSpan? PreviosSaveTime {
            get { return _previosSaveTime; }
            set {
                if (Equals(value, _previosSaveTime)) return;
                _previosSaveTime = value;
                OnPropertyChanged();
            }
        }

        private const byte DeflateFlag = 0;
        private const byte LzfFlag = 11;

        private string DecodeBytes(byte[] bytes) {
            if (bytes[0] == LzfFlag) {
                return Encoding.UTF8.GetString(Lzf.Decompress(bytes, 1, bytes.Length - 1));
            }

            var deflateMode = bytes[0] == DeflateFlag;

            if (!deflateMode && !bytes.Any(x => x < 0x20 && x != '\t' && x != '\n' && x != '\r')) {
                return Encoding.UTF8.GetString(bytes);
            }

            using (var inputStream = new MemoryStream(bytes)) {
                if (deflateMode) {
                    inputStream.Seek(1, SeekOrigin.Begin);
                }

                using (var gzip = new DeflateStream(inputStream, CompressionMode.Decompress)) {
                    using (var reader = new StreamReader(gzip, Encoding.UTF8)) {
                        return reader.ReadToEnd();
                    }
                }
            }
        }

        private byte[] EncodeBytes(string s) {
            var bytes = Encoding.UTF8.GetBytes(s);
            if (_disableCompression) return bytes;
            if (_useDeflate) {
                using (var output = new MemoryStream()) {
                    output.WriteByte(DeflateFlag);

                    using (var gzip = new DeflateStream(output, CompressionLevel.Fastest)) {
                        gzip.Write(bytes, 0, bytes.Length);
                    }

                    return output.ToArray();
                }
            }

            return Lzf.CompressWithPrefix(bytes, LzfFlag);
        }

        public static string EncodeBase64([NotNull] string s) {
            if (s == null) throw new ArgumentNullException(nameof(s));
            var plainTextBytes = Encoding.UTF8.GetBytes(s);
            return Convert.ToBase64String(plainTextBytes);
        }

        public static string DecodeBase64([NotNull] string s) {
            if (s == null) throw new ArgumentNullException(nameof(s));
            var base64EncodedBytes = Convert.FromBase64String(s);
            return Encoding.UTF8.GetString(base64EncodedBytes);
        }

        public static string Encode([NotNull] string s) {
            if (s == null) throw new ArgumentNullException(nameof(s));
            var result = new StringBuilder(s.Length + 5);
            foreach (var c in s) {
                switch (c) {
                    case '\\':
                        result.Append(@"\\");
                        break;

                    case '\n':
                        result.Append(@"\n");
                        break;

                    case '\t':
                        result.Append(@"\t");
                        break;

                    case '\b':
                    case '\r':
                        break;

                    default:
                        result.Append(c);
                        break;
                }
            }

            return result.ToString();
        }

        public static string Decode([NotNull] string s) {
            if (s == null) throw new ArgumentNullException(nameof(s));
            var result = new StringBuilder(s.Length);
            for (var i = 0; i < s.Length; i++) {
                var c = s[i];
                if (c != '\\') {
                    result.Append(c);
                    continue;
                }

                if (++i >= s.Length) {
                    break;
                }

                switch (s[i]) {
                    case '\\':
                        result.Append(@"\");
                        break;

                    case 'n':
                        result.Append("\n");
                        break;

                    case 't':
                        result.Append("\t");
                        break;
                }
            }

            return result.ToString();
        }

        private void Load() {
            var w = Stopwatch.StartNew();
            if (_filename == null || !File.Exists(_filename)) {
                _storage.Clear();
                return;
            }

            try {
                var splitted = DecodeBytes(File.ReadAllBytes(_filename))
                        .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                Load(int.Parse(splitted[0].Split(new[] { "version:" }, StringSplitOptions.None)[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture), splitted.Skip(1));
            } catch (Exception e) {
                Logging.Warning("Cannot load data: " + e);
                _storage.Clear();
            }

            OnPropertyChanged(nameof(Count));
            Logging.Write($"[ValuesStorage] Loading: {w.Elapsed.TotalMilliseconds:F2} ms");
        }

        private const int ActualVersion = 2;

        private void Load(int version, IEnumerable<string> data) {
            lock (_storage) {
                _storage.Clear();
                switch (version) {
                    case 2:
                        foreach (var split in data
                                .Select(line => line.Split(new[] { '\t' }, 2))
                                .Where(split => split.Length == 2)) {
                            _storage[Decode(split[0])] = Decode(split[1]);
                        }
                        break;

                    case 1:
                        foreach (var split in data
                                .Select(line => line.Split(new[] { '\t' }, 2))
                                .Where(split => split.Length == 2)) {
                            _storage[split[0]] = DecodeBase64(split[1]);
                        }
                        break;

                    default:
                        throw new InvalidDataException("Invalid version: " + version);
                }
            }
        }

        public string GetData() {
            lock (_storage) {
                return "version: " + ActualVersion + "\n" + string.Join("\n", from x in _storage
                                                                              where x.Key != null && x.Value != null
                                                                              select Encode(x.Key) + '\t' + Encode(x.Value));
            }
        }

        private void Save() {
            if (_filename == null) return;

            var data = GetData();
            try {
                File.WriteAllBytes(_filename, EncodeBytes(data));
            } catch (Exception e) {
                Logging.Write("Cannot save values: " + e);
            }
        }

        private async Task SaveAsync() {
            if (_filename == null) return;

            var sw = Stopwatch.StartNew();

            await Task.Run(() => {
                var data = GetData();
                try {
                    File.WriteAllBytes(_filename, EncodeBytes(data));
                } catch (Exception e) {
                    Logging.Write("Cannot save values: " + e);
                }
            });

            PreviosSaveTime = sw.Elapsed;
        }

        private static event EventHandler _exitEvent;

        public static void SaveBeforeExit() {
            _exitEvent?.Invoke(null, EventArgs.Empty);
        }

        private bool _dirty;

        private async void Dirty() {
            if (_dirty) return;

            try {
                _dirty = true;
                await Task.Delay(5000);
                await SaveAsync();
            } finally {
                _dirty = false;
            }
        }

        [CanBeNull, Pure]
        public string GetString([NotNull, LocalizationRequired(false)] string key, string defaultValue = null) {
            if (key == null) throw new ArgumentNullException(nameof(key));
            string value;
            return _storage.TryGetValue(key, out value) ? value : defaultValue;
        }

        [Pure]
        public T GetEnum<T>([NotNull, LocalizationRequired(false)] string key, T defaultValue = default(T)) where T : struct, IConvertible {
            if (key == null) throw new ArgumentNullException(nameof(key));
            T result;
            string value;
            return _storage.TryGetValue(key, out value) &&
                    Enum.TryParse(value, out result) ? result : defaultValue;
        }

        [Pure]
        public T? GetEnumNullable<T>([NotNull, LocalizationRequired(false)] string key) where T : struct, IConvertible {
            if (key == null) throw new ArgumentNullException(nameof(key));
            T result;
            string value;
            return _storage.TryGetValue(key, out value) &&
                    Enum.TryParse(value, out result) ? result : (T?)null;
        }

        [Pure]
        public int GetInt([NotNull, LocalizationRequired(false)] string key, int defaultValue = 0) {
            if (key == null) throw new ArgumentNullException(nameof(key));
            int result;
            string value;
            return _storage.TryGetValue(key, out value) &&
                    int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result) ? result : defaultValue;
        }

        [Pure]
        public int? GetIntNullable([NotNull, LocalizationRequired(false)] string key) {
            if (key == null) throw new ArgumentNullException(nameof(key));
            int result;
            string value;
            return _storage.TryGetValue(key, out value) &&
                    int.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result) ? result : (int?)null;
        }

        [Pure]
        public double GetDouble([NotNull, LocalizationRequired(false)] string key, double defaultValue = 0) {
            if (key == null) throw new ArgumentNullException(nameof(key));
            double result;
            string value;
            return _storage.TryGetValue(key, out value) &&
                    double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result) ? result : defaultValue;
        }

        [Pure]
        public double? GetDoubleNullable([NotNull, LocalizationRequired(false)] string key) {
            if (key == null) throw new ArgumentNullException(nameof(key));
            double result;
            string value;
            return _storage.TryGetValue(key, out value) &&
                    double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result) ? result : (double?)null;
        }

        [Pure]
        public Point GetPoint([NotNull, LocalizationRequired(false)] string key, Point defaultValue = default(Point)) {
            if (key == null) throw new ArgumentNullException(nameof(key));
            try {
                string value;
                if (_storage.TryGetValue(key, out value)) {
                    return Point.Parse(value);
                }
            } catch (Exception) {
                // ignored
            }
            return defaultValue;
        }

        [Pure]
        public Point? GetPointNullable([NotNull, LocalizationRequired(false)] string key) {
            if (key == null) throw new ArgumentNullException(nameof(key));
            try {
                string value;
                if (_storage.TryGetValue(key, out value)) {
                    return Point.Parse(value);
                }
            } catch (Exception) {
                // ignored
            }
            return null;
        }

        [Pure]
        public bool GetBool([NotNull, LocalizationRequired(false)] string key, bool defaultValue = false) {
            if (key == null) throw new ArgumentNullException(nameof(key));
            string value;
            return _storage.TryGetValue(key, out value) ? value == "1" : defaultValue;
        }

        [Pure]
        public bool? GetBoolNullable([NotNull, LocalizationRequired(false)] string key) {
            if (key == null) throw new ArgumentNullException(nameof(key));
            string value;
            return _storage.TryGetValue(key, out value) ? value == "1" : (bool?)null;
        }

        /// <summary>
        /// Read value as a strings list.
        /// </summary>
        /// <param name="key">Value key</param>
        /// <param name="defaultValue">Default value</param>
        /// <returns>List if exists, default value otherwise, empty list if default value is null</returns>
        [NotNull, Pure]
        public IEnumerable<string> GetStringList([NotNull, LocalizationRequired(false)] string key, IEnumerable<string> defaultValue = null) {
            if (key == null) throw new ArgumentNullException(nameof(key));
            string value;
            return _storage.TryGetValue(key, out value) && !string.IsNullOrEmpty(value)
                    ? value.Split('\n').Select(Decode) : defaultValue ?? new string[] { };
        }

        [NotNull, Pure]
        public Dictionary<string, string> GetDictionary([NotNull, LocalizationRequired(false)] string key) {
            if (key == null) throw new ArgumentNullException(nameof(key));

            var result = new Dictionary<string, string>();
            string k = null;
            foreach (var item in GetStringList(key)) {
                if (k == null) {
                    k = item;
                } else {
                    result[k] = item;
                    k = null;
                }
            }

            return result;
        }

        [Pure]
        public TimeSpan GetTimeSpan([NotNull, LocalizationRequired(false)] string key, TimeSpan defaultValue) {
            if (key == null) throw new ArgumentNullException(nameof(key));
            try {
                return TimeSpan.Parse(GetString(key));
            } catch (Exception) {
                return defaultValue;
            }
        }

        [Pure]
        public TimeSpan? GetTimeSpan([NotNull, LocalizationRequired(false)] string key) {
            if (key == null) throw new ArgumentNullException(nameof(key));
            try {
                return TimeSpan.Parse(GetString(key));
            } catch (Exception) {
                return null;
            }
        }

        [Pure]
        public DateTime GetDateTime([NotNull, LocalizationRequired(false)] string key, DateTime defaultValue) {
            if (key == null) throw new ArgumentNullException(nameof(key));
            try {
                return DateTime.Parse(GetString(key));
            } catch (Exception) {
                return defaultValue;
            }
        }

        [Pure]
        public DateTime GetDateTimeOrEpochTime([NotNull, LocalizationRequired(false)] string key) {
            if (key == null) throw new ArgumentNullException(nameof(key));
            try {
                return DateTime.Parse(GetString(key));
            } catch (Exception) {
                return new DateTime(1970, 1, 1);
            }
        }

        [Pure]
        public DateTime? GetDateTime([NotNull, LocalizationRequired(false)] string key) {
            if (key == null) throw new ArgumentNullException(nameof(key));
            try {
                return DateTime.Parse(GetString(key));
            } catch (Exception) {
                return null;
            }
        }

        [Pure, CanBeNull]
        public TimeZoneInfo GetTimeZoneInfo([NotNull, LocalizationRequired(false)] string key) {
            if (key == null) throw new ArgumentNullException(nameof(key));
            try {
                var value = GetString(key);
                return string.IsNullOrEmpty(value) ? null : TimeZoneInfo.FromSerializedString(value);
            } catch (Exception) {
                return null;
            }
        }

        [Pure, CanBeNull]
        public Uri GetUri([NotNull, LocalizationRequired(false)] string key, Uri defaultValue = null) {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (!Contains(key)) return defaultValue;
            try {
                return new Uri(_storage[key], UriKind.RelativeOrAbsolute);
            } catch (Exception e) {
                Logging.Warning("Cannot load uri: " + e);
                return defaultValue;
            }
        }

        [Pure]
        public Color? GetColor([NotNull, LocalizationRequired(false)] string key) {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (!Contains(key)) return null;
            try {
                var bytes = BitConverter.GetBytes(GetInt(key));
                return Color.FromArgb(bytes[0], bytes[1], bytes[2], bytes[3]);
            } catch (Exception e) {
                Logging.Warning("Cannot load uri: " + e);
                return null;
            }
        }

        [Pure]
        public Color GetColor([NotNull, LocalizationRequired(false)] string key, Color defaultValue) {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (!Contains(key)) return defaultValue;
            try {
                var bytes = BitConverter.GetBytes(GetInt(key));
                return Color.FromArgb(bytes[0], bytes[1], bytes[2], bytes[3]);
            } catch (Exception e) {
                Logging.Warning("Cannot load uri: " + e);
                return defaultValue;
            }
        }

        [Pure]
        public bool Contains([NotNull, LocalizationRequired(false)] string key) {
            if (key == null) throw new ArgumentNullException(nameof(key));
            return _storage.ContainsKey(key);
        }

        public void Set([NotNull, LocalizationRequired(false)] string key, string value) {
            lock (_storage) {
                string previous;
                var exists = _storage.TryGetValue(key, out previous);
                if (exists && previous == value) return;

                _storage[key] = value;
                Dirty();

                if (exists) {
                    OnPropertyChanged(nameof(Count));
                }
            }
        }

        public void Set([NotNull, LocalizationRequired(false)] string key, int value) {
            Set(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public void Set([NotNull, LocalizationRequired(false)] string key, double value) {
            Set(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public void Set([NotNull, LocalizationRequired(false)] string key, bool value) {
            Set(key, value ? "1" : "0");
        }

        public void Set([NotNull, LocalizationRequired(false)] string key, Point value) {
            Set(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public void Set([NotNull, LocalizationRequired(false)] string key, [NotNull] IEnumerable<string> value) {
            if (value == null) throw new ArgumentNullException(nameof(value));
            Set(key, string.Join("\n", value.Select(Encode)));
        }

        public void Set([NotNull, LocalizationRequired(false)] string key, [NotNull] IReadOnlyDictionary<string, string> value) {
            if (value == null) throw new ArgumentNullException(nameof(value));
            Set(key, string.Join("\n", value.SelectMany(x => new[] { Encode(x.Key), Encode(x.Value) })));
        }

        public void Set([NotNull, LocalizationRequired(false)] string key, TimeSpan timeSpan) {
            Set(key, timeSpan.ToString());
        }

        public void Set([NotNull, LocalizationRequired(false)] string key, DateTime dateTime) {
            Set(key, dateTime.ToString(CultureInfo.InvariantCulture));
        }

        public void Set([NotNull, LocalizationRequired(false)] string key, [NotNull] TimeZoneInfo timeZone) {
            if (timeZone == null) throw new ArgumentNullException(nameof(timeZone));
            Set(key, timeZone.ToSerializedString());
        }

        public void Set([NotNull, LocalizationRequired(false)] string key, [NotNull] Uri uri) {
            if (uri == null) throw new ArgumentNullException(nameof(uri));
            Set(key, uri.ToString());
        }

        public void Set([NotNull, LocalizationRequired(false)] string key, Color color) {
            Set(key, BitConverter.ToInt32(new[] { color.A, color.R, color.G, color.B }, 0));
        }

        public void SetEnum<T>([NotNull, LocalizationRequired(false)] string key, T value) where T : struct, IConvertible {
            Set(key, value.ToString(CultureInfo.InvariantCulture));
        }

        /* I know that this is not a proper protection or anything, but I just don’t want to save some
            stuff plain-texted */
        private const string Something = "encisfinedontworry";

        [Pure, CanBeNull]
        public string Decrypt([NotNull, LocalizationRequired(false)] string key, [NotNull, LocalizationRequired(false)] string value) {
            if (_encryptionKey == null) return value;
            var result = StringCipher.Decrypt(value, key + _encryptionKey);
            return result == null ? null : result.EndsWith(Something) ? result.Substring(0, result.Length - Something.Length) : null;
        }

        public string GetEncryptedString([NotNull, LocalizationRequired(false)] string key, string defaultValue = null) {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (_encryptionKey == null || !_storage.ContainsKey(key)) return defaultValue;
            var result = StringCipher.Decrypt(GetString(key, defaultValue), key + _encryptionKey);
            return result == null ? null : result.EndsWith(Something) ? result.Substring(0, result.Length - Something.Length) : defaultValue;
        }

        public bool GetEncryptedBool([NotNull, LocalizationRequired(false)] string key, bool defaultValue = false) {
            if (key == null) throw new ArgumentNullException(nameof(key));
            return _storage.ContainsKey(key) ? GetEncryptedString(key) == "1" : defaultValue;
        }

        public void SetEncrypted([NotNull, LocalizationRequired(false)] string key, string value) {
            if (_encryptionKey == null) return;
            var encrypted = StringCipher.Encrypt(value + Something, key + _encryptionKey);
            if (encrypted == null) {
                Remove(key);
            } else {
                Set(key, encrypted);
            }
        }

        public void SetEncrypted([NotNull, LocalizationRequired(false)] string key, bool value) {
            SetEncrypted(key, value ? "1" : "0");
        }

        public void Remove([NotNull, LocalizationRequired(false)] string key) {
            lock (_storage) {
                if (_storage.ContainsKey(key)) {
                    _storage.Remove(key);
                    Dirty();
                    OnPropertyChanged(nameof(Count));
                }
            }
        }

        public void CleanUp(Func<string, bool> predicate) {
            lock (_storage) {
                var keys = _storage.Keys.ToList();
                foreach (var key in keys.Where(predicate)) {
                    _storage.Remove(key);
                }

                Dirty();
                OnPropertyChanged(nameof(Count));
            }
        }
    }
}