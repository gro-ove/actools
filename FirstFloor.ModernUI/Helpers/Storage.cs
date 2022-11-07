using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FirstFloor.ModernUI.Presentation;
using FirstFloor.ModernUI.Serialization;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    [Localizable(false)]
    public class Storage : NotifyPropertyChanged, IStorage, IDisposable {
        [CanBeNull]
        public static string TemporaryBackupsDirectory { get; set; }

        private readonly Dictionary<string, string> _storage;
        private readonly string _filename;
        private readonly string _encryptionKey;
        private readonly bool _disableCompression;
        private readonly bool _useDeflate;
        private readonly int _sizeLimit;

        public Storage(string filename = null, string encryptionKey = null, bool disableCompression =
#if DEBUG
                true,
#else
                false,
#endif
                bool useDeflate = false, int sizeLimit = int.MaxValue) {
            _storage = new Dictionary<string, string>();
            _filename = filename;
            _encryptionKey = encryptionKey;
            _disableCompression = disableCompression;
            _useDeflate = useDeflate;
            _sizeLimit = sizeLimit;

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

        private TimeSpan? _previousSaveTime;

        public TimeSpan? PreviousSaveTime {
            get => _previousSaveTime;
            set => Apply(value, ref _previousSaveTime);
        }

        private const byte DeflateFlag = 0;
        private const byte LzfFlag = 11;

        private byte[] DecodeBytes(byte[] bytes) {
            if (bytes[0] == LzfFlag) {
                return Lzf.Decompress(bytes, 1, bytes.Length - 1);
            }

            var deflateMode = bytes[0] == DeflateFlag;
            if (!deflateMode && !bytes.Any(x => x < 0x20 && x != '\t' && x != '\n' && x != '\r')) {
                return bytes;
            }

            using (var inputStream = new MemoryStream(bytes)) {
                if (deflateMode) {
                    inputStream.Seek(1, SeekOrigin.Begin);
                }

                using (var gzip = new DeflateStream(inputStream, CompressionMode.Decompress))
                using (var memory = new MemoryStream()) {
                    gzip.CopyTo(memory);
                    return memory.ToArray();
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

        public static void EncodeTo([NotNull] StringBuilder dst, [NotNull] string s) {
            if (s == null) throw new ArgumentNullException(nameof(s));
            for (var i = 0; i < s.Length; i++) {
                var c = s[i];
                switch (c) {
                    case '\\':
                        dst.Append(@"\\");
                        break;
                    case '\n':
                        dst.Append(@"\n");
                        break;
                    case '\t':
                        dst.Append(@"\t");
                        break;
                    case '\b':
                    case '\r':
                        break;
                    default:
                        dst.Append(c);
                        break;
                }
            }
        }

        public static string Encode([NotNull] string s) {
            if (s == null) throw new ArgumentNullException(nameof(s));
            var result = new StringBuilder(s.Length + 5);
            EncodeTo(result, s);
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

            var filenameToLoad = _filename;
            if (filenameToLoad == null) {
                lock (_storage) {
                    _storage.Clear();
                }
                return;
            }

            var filenameToLoadExists = File.Exists(filenameToLoad);
            var backupFilename = GetBackupFilename();
            byte[] decodedLines = null;
            if (backupFilename != null && File.Exists(backupFilename)) {
                if (!filenameToLoadExists || new FileInfo(backupFilename).Length > new FileInfo(filenameToLoad).Length * 4) {
                    filenameToLoad = backupFilename;
                    filenameToLoadExists = true;
                } else {
                    try {
                        decodedLines = DecodeLines(filenameToLoad);
                        if (decodedLines.Length == 0) {
                            throw new Exception("Surprisingly empty");
                        }
                    } catch (Exception e) {
                        Logging.Warning(e);
                        filenameToLoad = backupFilename;
                    }
                }
            }

            if (!filenameToLoadExists) {
                lock (_storage) {
                    _storage.Clear();
                }
                return;
            }

            lock (_storage) {
                try {
                    var splitted = decodedLines ?? DecodeLines(filenameToLoad);
                    if (splitted.Length > _sizeLimit) {
                        Logging.Debug($"Choosing not to load {_filename}: file is too large already");
                        _storage.Clear();
                    } else if (!Load(splitted)) {
                        Logging.Warning($"Failed to load {_filename}: incorrect version");
                    }
                } catch (Exception e) {
                    Logging.Warning($"Failed to load {_filename}: {e}");
                    _storage.Clear();
                }
            }

            OnPropertyChanged(nameof(Count));
            if (w.Elapsed.TotalMilliseconds > 2) {
                Logging.Write($"{Path.GetFileName(_filename)}: {w.Elapsed.TotalMilliseconds:F2} ms");
            }

            byte[] DecodeLines(string filename) {
                var bytes = File.ReadAllBytes(filename);
                return bytes.Length == 0 ? new byte[0] : DecodeBytes(bytes);
            }
        }

        private const int ActualVersion = 2;

        private static char[] _chars = new char[256];

        public static string Decode(byte[] d, int f, int l) {
            if (l > _chars.Length) _chars = new char[l + 64];

            StringBuilder result = null;
            var u = 0;
            for (var i = 0; i < l; i++) {
                var c = d[f + i];
                if (c != '\\') {
                    ++u;
                    continue;
                }

                if (result == null) result = new StringBuilder(l);
                if (u > 0) {
                    var count = Encoding.UTF8.GetChars(d, f + i - u, u, _chars, 0);
                    result.Append(_chars, 0, count);
                    u = 0;
                }

                if (++i >= l) {
                    break;
                }

                switch (d[f + i]) {
                    case (byte)'\\':
                        result.Append('\\');
                        break;

                    case (byte)'n':
                        result.Append('\n');
                        break;

                    case (byte)'t':
                        result.Append('\t');
                        break;
                }
            }

            if (u > 0) {
                if (result == null) return Encoding.UTF8.GetString(d, f + l - u, u);
                var count = Encoding.UTF8.GetChars(d, f + l - u, u, _chars, 0);
                result.Append(_chars, 0, count);
            }

            return result?.ToString() ?? string.Empty;
        }

        private bool Load(byte[] data) {
            _storage.Clear();

            if (data.Length < 10
                    || data[0] != 'v' || data[1] != 'e' || data[7] != ':' || data[8] != ' '
                    || data.Length != 10 && data[10] != '\r' && data[10] != '\n') {
                return false;
            }

            switch (data[9] - '0') {
                case 2: {
                    LoadV2(data);
                    return true;
                }
                case 1: {
                    LoadV1(data);
                    return true;
                }
            }
            return false;
        }

        private void LoadV1(byte[] data) {
            var s = -1;
            var d = -1;
            for (var i = 10; i < data.Length; ++i) {
                var c = data[i];
                if (c == '\r' || c == '\n') {
                    if (s != -1 && d != -1) {
                        _storage[Decode(data, s, d - s)] = DecodeBase64(Encoding.ASCII.GetString(data, d + 1, i - d - 1));
                    }
                    s = i + 1;
                    d = -1;
                }
                if (c == '\t') {
                    d = i;
                }
            }
            if (s != -1 && d != -1) {
                var i = data.Length;
                _storage[Decode(data, s, d - s)] = DecodeBase64(Encoding.ASCII.GetString(data, d + 1, i - d - 1));
            }
        }

        private void LoadV2(byte[] data) {
            var s = -1;
            var d = -1;
            for (var i = 10; i < data.Length; ++i) {
                var c = data[i];
                if (c == '\r' || c == '\n') {
                    if (s != -1 && d != -1) {
                        _storage[Decode(data, s, d - s)] = Decode(data, d + 1, i - d - 1);
                    }
                    s = i + 1;
                    d = -1;
                }
                if (c == '\t') {
                    d = i;
                }
            }

            if (s != -1 && d != -1) {
                var i = data.Length;
                _storage[Decode(data, s, d - s)] = Decode(data, d + 1, i - d - 1);
            }
        }

        public string GetData() {
            lock (_storage) {
                var ret = new StringBuilder();
                ret.Append("version: ");
                ret.Append(ActualVersion);
                foreach (var p in _storage) {
                    if (p.Key != null && p.Value != null) {
                        ret.Append('\n');
                        EncodeTo(ret, p.Key);
                        ret.Append('\t');
                        EncodeTo(ret, p.Value);
                    }
                }
                return ret.ToString();
            }
        }

        [NotNull]
        private static string EnsureUnique([NotNull] string filename, int startFrom = 1) {
            if (!File.Exists(filename) && !Directory.Exists(filename)) {
                return filename;
            }

            var ext = Path.GetExtension(filename);
            var start = filename.Substring(0, filename.Length - ext.Length);

            for (var i = startFrom; i < 99999; i++) {
                var result = $"{start}-{i}{ext}";
                if (File.Exists(result) || Directory.Exists(result)) continue;
                return result;
            }

            throw new Exception("Can’t find unique filename");
        }

        [CanBeNull]
        private string GetBackupFilename() {
            return TemporaryBackupsDirectory == null || _filename == null ? null : Path.Combine(TemporaryBackupsDirectory, Path.GetFileName(_filename));
        }

        private void SaveData(string data) {
            try {
                var filename = _filename;
                if (filename == null) return;

                var bytes = Encoding.UTF8.GetBytes(data);
                var newFilename = EnsureUnique(_filename + ".new");

                if (_disableCompression) {
                    File.WriteAllBytes(newFilename, bytes);
                } else {
                    using (var output = new FileStream(newFilename, FileMode.Create, FileAccess.Write, FileShare.ReadWrite)) {
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
                }

                if (newFilename == filename) return;

                if (File.Exists(filename)) {
                    var backupFilename = GetBackupFilename();
                    if (backupFilename != null) {
                        try {
                            if (File.Exists(backupFilename)) {
                                File.Delete(backupFilename);
                            }
                            File.Move(filename, backupFilename);
                        } catch (Exception e) {
                            Logging.Error(e);
                        }
                    } else {
                        File.Delete(filename);
                    }

                    try {
                        if (File.Exists(filename)) {
                            File.Delete(filename);
                        }
                    } catch (Exception e) {
                        Logging.Error(e);
                    }
                }

                File.Move(newFilename, filename);
            } catch (Exception e) {
                Logging.Error(e);
            }
        }

        private void Save() {
            if (_filename == null) return;
            SaveData(GetData());
        }

        public void ForceSave() {
            SaveData(GetData());
        }

        private async Task SaveAsync() {
            if (_filename == null) return;
            var sw = Stopwatch.StartNew();
            var data = GetData();
            await Task.Run(() => SaveData(data));
            PreviousSaveTime = sw.Elapsed;
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

        [ContractAnnotation("defaultValue:null => canbenull; defaultValue:notnull => notnull")]
        public T Get<T>(string key, T defaultValue = default) {
            if (key == null) throw new ArgumentNullException(nameof(key));
            lock (_storage) {
                return _storage.TryGetValue(key, out var value) ? value.As<T>() : defaultValue;
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
        public bool Contains([LocalizationRequired(false)] string key) {
            if (key == null) throw new ArgumentNullException(nameof(key));
            lock (_storage) {
                return _storage.ContainsKey(key);
            }
        }

        public void Set([LocalizationRequired(false)] string key, object value) {
            var v = value.AsShort<string>();
            lock (_storage) {
                var exists = _storage.TryGetValue(key, out var previous);
                if (exists && previous == v) return;

                _storage[key] = v;
                Dirty();

                if (exists) {
                    OnPropertyChanged(nameof(Count));
                }
            }
        }

        public void SetStringList(string key, IEnumerable<string> value) {
            if (value == null) throw new ArgumentNullException(nameof(value));
            Set(key, string.Join("\n", value.Select(Encode)));
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

        public T GetEncrypted<T>([LocalizationRequired(false)] string key, T defaultValue = default) {
            if (key == null) throw new ArgumentNullException(nameof(key));

            if (_encryptionKey == null || !Contains(key)) return defaultValue;
            var result = StringCipher.Decrypt(Get<string>(key), key + _encryptionKey);
            return result?.EndsWith(Something) == true ? result.Substring(0, result.Length - Something.Length).As(defaultValue) : defaultValue;
        }

        public void SetEncrypted([LocalizationRequired(false)] string key, object value) {
            if (_encryptionKey == null) {
                Logging.Warning($"Encryption key is not set to save {key}");
                return;
            }

            var encrypted = StringCipher.Encrypt(value.AsShort<string>() + Something, key + _encryptionKey);
            if (encrypted == null) {
                Remove(key);
            } else {
                Set(key, encrypted);
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
            ActionExtension.InvokeInMainThread(() => { OnPropertyChanged(nameof(Count)); });
        }

        public void CopyFrom(Storage source) {
            lock (_storage) {
                _storage.Clear();
                foreach (var pair in source) {
                    _storage[pair.Key] = pair.Value;
                }
            }

            Dirty();
            ActionExtension.InvokeInMainThread(() => { OnPropertyChanged(nameof(Count)); });
        }

        public IEnumerable<string> Keys {
            get {
                lock (_storage) {
                    return _storage.Keys;
                }
            }
        }

        public void Clear() {
            lock (_storage) {
                _storage.Clear();
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