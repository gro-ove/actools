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
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
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

        public int Count {
            get {
                lock (_storage) {
                    return _storage.Count;
                }
            }
        }

        private TimeSpan? _previosSaveTime;

        public TimeSpan? PreviosSaveTime {
            get => _previosSaveTime;
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

        [NotNull, Pure]
        public static string EncodeList([CanBeNull, ItemCanBeNull] params string[] list) {
            return EncodeList((IEnumerable<string>)list);
        }

        [NotNull, Pure]
        public static string EncodeList([CanBeNull, ItemCanBeNull] IEnumerable<string> list) {
            return list == null ? "" : string.Join("\n", list.Where(x => !string.IsNullOrEmpty(x)).Select(Encode));
        }

        [NotNull, ItemNotNull, Pure]
        public static IEnumerable<string> DecodeList([CanBeNull] string encoded) {
            return encoded?.Split('\n').Where(x => !string.IsNullOrEmpty(x)).Select(Decode) ?? new string[0];
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
                lock (_storage) {
                    _storage.Clear();
                }
                return;
            }

            try {
                var splitted = DecodeBytes(File.ReadAllBytes(_filename))
                        .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                Load(int.Parse(splitted[0].Split(new[] { "version:" }, StringSplitOptions.None)[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture),
                        splitted.Skip(1));
            } catch (Exception e) {
                Logging.Warning(e);
                lock (_storage) {
                    _storage.Clear();
                }
            }

            OnPropertyChanged(nameof(Count));
            Logging.Write($"{Path.GetFileName(_filename)}: {w.Elapsed.TotalMilliseconds:F2} ms");
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
                return _storage.TryGetValue(key, out var value) ? value : defaultValue;
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
                var exists = _storage.TryGetValue(key, out var previous);
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

        public IEnumerable<string> Keys {
            get {
                lock (_storage) {
                    return _storage.Keys;
                }
            }
        }

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