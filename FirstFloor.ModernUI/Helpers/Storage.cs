using System;
using System.Collections;
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
    public interface IStorage : IEnumerable<KeyValuePair<string, string>> {
        [Pure]
        bool Contains([NotNull, LocalizationRequired(false)] string key);

        [CanBeNull, Pure]
        string GetString([NotNull, LocalizationRequired(false)] string key, string defaultValue = null);

        [NotNull, Pure]
        IEnumerable<string> GetStringList([NotNull, LocalizationRequired(false)] string key, IEnumerable<string> defaultValue = null);

        [CanBeNull, Pure]
        string GetEncryptedString([NotNull, LocalizationRequired(false)] string key, string defaultValue = null);

        void SetString([NotNull, LocalizationRequired(false)] string key, string value);

        void SetStringList([NotNull, LocalizationRequired(false)] string key, [NotNull] IEnumerable<string> value);

        void SetEncryptedString([NotNull, LocalizationRequired(false)] string key, string value);

        bool Remove([NotNull, LocalizationRequired(false)] string key);

        IEnumerable<string> Keys { get; }
    }

    public class Substorage : IStorage {
        private readonly IStorage _baseStorage;
        private readonly string _prefix;

        public Substorage([NotNull] IStorage baseStorage, [NotNull, LocalizationRequired(false)] string prefix) {
            _baseStorage = baseStorage;
            _prefix = prefix;
        }

        private class Enumerator : IEnumerator<KeyValuePair<string, string>> {
            private readonly IStorage _baseStorage;
            private readonly string _prefix;
            private IEnumerator<KeyValuePair<string, string>> _enumerator;
            private KeyValuePair<string, string> _current;

            public Enumerator(IStorage baseStorage, string prefix) {
                _baseStorage = baseStorage;
                _prefix = prefix;
            }

            public void Dispose() {
                if (_enumerator != null) {
                    _enumerator.Dispose();
                    _enumerator = null;
                }
            }

            public bool MoveNext() {
                if (_enumerator == null) {
                    _enumerator = _baseStorage.GetEnumerator();
                }

                KeyValuePair<string, string> current;
                do {
                    if (!_enumerator.MoveNext()) return false;
                    current = _enumerator.Current;
                } while (!current.Key.StartsWith(_prefix));
                
                _current = new KeyValuePair<string, string>(current.Key.Substring(_prefix.Length), current.Value);
                return true;
            }

            public void Reset() {
                if (_enumerator != null) {
                    _enumerator.Dispose();
                    _enumerator = null;
                }
            }

            public KeyValuePair<string, string> Current => _current;

            object IEnumerator.Current => _current;
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() {
            return new Enumerator(_baseStorage, _prefix);
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public bool Contains(string key) {
            return _baseStorage.Contains(_prefix + key);
        }

        public string GetString(string key, string defaultValue = null) {
            return _baseStorage.GetString(_prefix + key, defaultValue);
        }

        public IEnumerable<string> GetStringList(string key, IEnumerable<string> defaultValue = null) {
            return _baseStorage.GetStringList(_prefix + key, defaultValue);
        }

        public string GetEncryptedString(string key, string defaultValue = null) {
            return _baseStorage.GetString(_prefix + key, defaultValue);
        }

        public void SetString(string key, string value) {
            _baseStorage.SetString(_prefix + key, value);
        }

        public void SetStringList(string key, IEnumerable<string> value) {
            _baseStorage.SetStringList(_prefix + key, value);
        }

        public void SetEncryptedString(string key, string value) {
            _baseStorage.SetEncryptedString(_prefix + key, value);
        }

        public bool Remove(string key) {
            return _baseStorage.Remove(_prefix + key);
        }

        public IEnumerable<string> Keys => _baseStorage.Keys.Where(x => x.StartsWith(_prefix)).Select(x => x.Substring(_prefix.Length));
    }

    public static class StorageMethods {
        [Pure]
        public static T GetEnum<T>([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, T defaultValue = default(T)) where T : struct, IConvertible {
            return GetEnumNullable<T>(storage, key) ?? defaultValue;
        }

        [Pure]
        public static T? GetEnumNullable<T>([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key) where T : struct, IConvertible {
            T result;
            var value = storage.GetString(key);
            return value != null && Enum.TryParse(value, out result) ? result : (T?)null;
        }

        [Pure]
        public static int GetInt([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, int defaultValue = 0) {
            return GetIntNullable(storage, key) ?? defaultValue;
        }

        [Pure]
        public static int? GetIntNullable([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key) {
            int result;
            var value = storage.GetString(key);
            return value != null && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result) ? result : (int?)null;
        }

        [Pure]
        public static double GetDouble([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, double defaultValue = 0) {
            return GetDoubleNullable(storage, key) ?? defaultValue;
        }

        [Pure]
        public static double? GetDoubleNullable([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key) {
            double result;
            var value = storage.GetString(key);
            return value != null && double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result) ? result : (double?)null;
        }

        [Pure]
        public static bool GetBool([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, bool defaultValue = false) {
            return GetBoolNullable(storage, key) ?? defaultValue;
        }

        [Pure]
        public static bool? GetBoolNullable([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key) {
            var value = storage.GetString(key);
            return value != null ? value == @"1" : (bool?)null;
        }

        [Pure]
        public static TimeSpan GetTimeSpan([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, TimeSpan defaultValue) {
            return storage.GetTimeSpan(key) ?? defaultValue;
        }

        [Pure]
        public static TimeSpan? GetTimeSpan([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key) {
            try {
                var value = storage.GetString(key);
                return value == null ? (TimeSpan?)null : TimeSpan.Parse(storage.GetString(key), CultureInfo.InvariantCulture);
            } catch (Exception e) {
                Logging.Error(e);
                return null;
            }
        }

        [Pure]
        public static DateTime GetDateTime([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, DateTime defaultValue) {
            return storage.GetDateTime(key) ?? defaultValue;
        }

        [Pure]
        public static DateTime GetDateTimeOrEpochTime([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key) {
            try {
                var value = storage.GetString(key);
                return value == null ? new DateTime(1970, 1, 1) : DateTime.Parse(storage.GetString(key), CultureInfo.InvariantCulture);
            } catch (Exception e) {
                Logging.Error(e);
                return new DateTime(1970, 1, 1);
            }
        }

        [Pure]
        public static DateTime? GetDateTime([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key) {
            try {
                var value = storage.GetString(key);
                return value == null ? (DateTime?)null : DateTime.Parse(storage.GetString(key), CultureInfo.InvariantCulture);
            } catch (Exception e) {
                Logging.Error(e);
                return null;
            }
        }

        [Pure, CanBeNull]
        public static TimeZoneInfo GetTimeZoneInfo([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key) {
            try {
                var value = storage.GetString(key);
                return value == null ? null : TimeZoneInfo.FromSerializedString(value);
            } catch (Exception e) {
                Logging.Error(e);
                return null;
            }
        }

        [NotNull, Pure]
        public static Dictionary<string, string> GetDictionary([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key) {
            if (key == null) throw new ArgumentNullException(nameof(key));

            var result = new Dictionary<string, string>();
            string k = null;
            foreach (var item in storage.GetStringList(key)) {
                if (k == null) {
                    k = item;
                } else {
                    result[k] = item;
                    k = null;
                }
            }

            return result;
        }

        [Pure, CanBeNull]
        public static Uri GetUri([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, Uri defaultValue = null) {
            try {
                var value = storage.GetString(key);
                return value == null ? defaultValue : new Uri(value, UriKind.RelativeOrAbsolute);
            } catch (Exception e) {
                Logging.Error(e);
                return defaultValue;
            }
        }

        public static void Set([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, string value) {
            storage.SetString(key, value);
        }

        public static void Set([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, IEnumerable<string> value) {
            storage.SetStringList(key, value);
        }

        public static void Set([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, int value) {
            storage.SetString(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public static void Set([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, double value) {
            storage.SetString(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public static void Set([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, bool value) {
            storage.SetString(key, value ? @"1" : @"0");
        }

        public static void SetNonDefault([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, bool value) {
            if (value) {
                storage.SetString(key, @"1");
            } else {
                storage.Remove(key);
            }
        }
        
        public static void Set([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, [NotNull] IReadOnlyDictionary<string, string> value) {
            if (value == null) throw new ArgumentNullException(nameof(value));
            storage.SetStringList(key, value.SelectMany(x => new[] { x.Key, x.Value }));
        }

        public static void Set([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, TimeSpan timeSpan) {
            storage.SetString(key, timeSpan.ToString());
        }

        public static void Set([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, DateTime dateTime) {
            storage.SetString(key, dateTime.ToString(CultureInfo.InvariantCulture));
        }

        public static void Set([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, [NotNull] TimeZoneInfo timeZone) {
            if (timeZone == null) throw new ArgumentNullException(nameof(timeZone));
            storage.SetString(key, timeZone.ToSerializedString());
        }

        public static void Set([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, [NotNull] Uri uri) {
            if (uri == null) throw new ArgumentNullException(nameof(uri));
            storage.SetString(key, uri.ToString());
        }

        public static void SetEnum<T>([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, T value) where T : struct, IConvertible {
            storage.SetString(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public static string GetEncrypted([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, string defaultValue = null) {
            return storage.GetEncryptedString(key, defaultValue);
        }

        public static bool GetEncryptedBool([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, bool defaultValue = false) {
            if (key == null) throw new ArgumentNullException(nameof(key));
            return storage.Contains(key) ? storage.GetEncryptedString(key) == @"1" : defaultValue;
        }

        public static void SetEncrypted([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, string value) {
            storage.SetEncryptedString(key, value);
        }

        public static void SetEncrypted([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, bool value) {
            storage.SetEncryptedString(key, value ? @"1" : @"0");
        }

        public static IStorage GetSubstorage([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string prefix) {
            return new Substorage(storage, prefix);
        }
    }

    public static class StorageWpfMethods {
        [Pure]
        public static Point GetPoint([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, Point defaultValue = default(Point)) {
            return GetPointNullable(storage, key) ?? defaultValue;
        }

        [Pure]
        public static Point? GetPointNullable([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key) {
            try {
                var value = storage.GetString(key);
                return value == null ? (Point?)null : Point.Parse(value);
            } catch (Exception e) {
                Logging.Error(e);
                return null;
            }
        }

        [Pure]
        public static Color? GetColor([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key) {
            if (!storage.Contains(key)) return null;
            try {
                var bytes = BitConverter.GetBytes(storage.GetInt(key));
                return Color.FromArgb(bytes[0], bytes[1], bytes[2], bytes[3]);
            } catch (Exception e) {
                Logging.Error(e);
                return null;
            }
        }

        [Pure]
        public static Color GetColor([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, Color defaultValue) {
            return GetColor(storage, key) ?? defaultValue;
        }

        public static void Set([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, Point value) {
            storage.SetString(key, value.ToString(CultureInfo.InvariantCulture));
        }

        public static void Set([NotNull] this IStorage storage, [NotNull, LocalizationRequired(false)] string key, Color color) {
            storage.Set(key, BitConverter.ToInt32(new[] { color.A, color.R, color.G, color.B }, 0));
        }
    }

    [Localizable(false)]
    public class Storage : NotifyPropertyChanged, IStorage, IDisposable {
        private readonly Dictionary<string, string> _storage;
        private readonly string _filename;
        private readonly string _encryptionKey;
        private readonly bool _disableCompression;
        private readonly bool _useDeflate;

        public Storage(string filename = null, string encryptionKey = null, bool disableCompression =
#if DEBUG
                true,
#else
                false,
#endif
                bool useDeflate = false) {
            _storage = new Dictionary<string, string>();
            _filename = filename;
            _encryptionKey = encryptionKey;
            _disableCompression = disableCompression;
            _useDeflate = useDeflate;

            Load();
            Exit += OnExit;
        }

        private void OnExit(object sender, EventArgs e) {
            if (_dirty) {
                Save();
            }
        }

        public void Dispose() {
            if (_dirty) {
                Save();
            }

            Exit -= OnExit;
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

            using (var output = new MemoryStream()) {
                output.WriteByte(DeflateFlag);

                if (_useDeflate) {
                    using (var gzip = new DeflateStream(output, CompressionLevel.Fastest)) {
                        gzip.Write(bytes, 0, bytes.Length);
                    }
                } else {
                    Lzf.Compress(bytes, bytes.Length, output);
                }

                return output.ToArray();
            }
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
            Logging.Write($"Loading: {w.Elapsed.TotalMilliseconds:F2} ms");
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

        private void SaveData(string data) {
            try {
                var bytes = Encoding.UTF8.GetBytes(data);
                if (_disableCompression) {
                    File.WriteAllBytes(_filename, bytes);
                    return;
                }

                using (var output = new FileStream(_filename, FileMode.Create, FileAccess.Write, FileShare.Read)) {
                    if (_useDeflate) {
                        output.WriteByte(DeflateFlag);
                        using (var gzip = new DeflateStream(output, CompressionLevel.Fastest)) {
                            gzip.Write(bytes, 0, bytes.Length);
                        }
                    } else {
                        output.WriteByte(LzfFlag);
                        Lzf.Compress(bytes, bytes.Length, output);
                    }
                }
            } catch (Exception e) {
                Logging.Error(e);
            }
        }

        private void Save() {
            if (_filename == null) return;
            SaveData(GetData());
        }

        private async Task SaveAsync() {
            if (_filename == null) return;
            var sw = Stopwatch.StartNew();
            var data = GetData();
            await Task.Run(() => SaveData(data));
            PreviosSaveTime = sw.Elapsed;
        }

        public static event EventHandler Exit;

        public static void SaveBeforeExit() {
            Exit?.Invoke(null, EventArgs.Empty);
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

        [Pure]
        public string GetString([LocalizationRequired(false)] string key, string defaultValue = null) {
            if (key == null) throw new ArgumentNullException(nameof(key));
            lock (_storage) {
                string value;
                return _storage.TryGetValue(key, out value) ? value : defaultValue;
            }
        }

        public IEnumerable<string> GetStringList(string key, IEnumerable<string> defaultValue = null) {
            string value;

            lock (_storage) {
                if (!_storage.TryGetValue(key, out value) || string.IsNullOrEmpty(value)) {
                    return defaultValue ?? new string[] { };
                }
            }

            var s = value.Split('\n');
            for (var i = s.Length - 1; i >= 0; i--) {
                s[i] = Decode(s[i]);
            }
            return s;
        }

        [Pure]
        public bool Contains([ LocalizationRequired(false)] string key) {
            if (key == null) throw new ArgumentNullException(nameof(key));
            lock (_storage) {
                return _storage.ContainsKey(key);
            }
        }

        public void SetString([LocalizationRequired(false)] string key, string value) {
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

        public void SetStringList(string key, IEnumerable<string> value) {
            if (value == null) throw new ArgumentNullException(nameof(value));
            SetString(key, string.Join("\n", value.Select(Encode)));
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

        public string GetEncryptedString([LocalizationRequired(false)] string key, string defaultValue = null) {
            if (key == null) throw new ArgumentNullException(nameof(key));

            if (_encryptionKey == null || !Contains(key)) return defaultValue;
            var result = StringCipher.Decrypt(GetString(key, defaultValue), key + _encryptionKey);
            return result == null ? null : result.EndsWith(Something) ? result.Substring(0, result.Length - Something.Length) : defaultValue;
        }

        public void SetEncryptedString([LocalizationRequired(false)] string key, string value) {
            if (_encryptionKey == null) return;
            var encrypted = StringCipher.Encrypt(value + Something, key + _encryptionKey);
            if (encrypted == null) {
                Remove(key);
            } else {
                SetString(key, encrypted);
            }
        }

        public bool Remove([LocalizationRequired(false)] string key) {
            lock (_storage) {
                if (_storage.ContainsKey(key)) {
                    _storage.Remove(key);
                    Dirty();
                    OnPropertyChanged(nameof(Count));
                    return true;
                }
            }

            return false;
        }

        public void CleanUp(Func<string, bool> predicate) {
            lock (_storage) {
                var keys = _storage.Keys.ToList();
                foreach (var key in keys.Where(predicate)) {
                    _storage.Remove(key);
                }
            }

            Dirty();
            ActionExtension.InvokeInMainThread(() => {
                OnPropertyChanged(nameof(Count));
            });
        }

        public void CopyFrom(Storage source) {
            lock (_storage) {
                _storage.Clear();
                foreach (var pair in source) {
                    _storage[pair.Key] = pair.Value;
                }
            }

            Dirty();
            ActionExtension.InvokeInMainThread(() => {
                OnPropertyChanged(nameof(Count));
            });
        }

        public IEnumerable<string> Keys => _storage.Keys;

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() {
            lock (_storage) {
                return _storage.GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }
}