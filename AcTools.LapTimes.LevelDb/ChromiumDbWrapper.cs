using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using AcTools.LapTimes.LevelDb.LevelDbUtils;

namespace AcTools.LapTimes.LevelDb {
    internal class ChromiumDbWrapper : IDisposable {
        private readonly LevelDbUtils.LevelDb _levelDb;
        private readonly Comparator _comparator;
        private readonly Options _options;

        public ChromiumDbWrapper(string directory) {
            _comparator = Comparator.Create("idb_cmp1",
                    (xs, ys) => Compare(new StringPiece(xs), new StringPiece(ys), false));

            _options = new Options {
                Comparator = _comparator
            };

            _levelDb = new LevelDbUtils.LevelDb(_options, directory);
        }

        public IEnumerable<Dictionary<string, string>> GetData() {
            return GetBytesData().Select(x => {
                try {
                    return ParseBits(x);
                } catch (Exception e) {
                    AcToolsLogging.Write("ERROR: " + e);
                    return null;
                }
            }).Where(x => x != null);
        }

        public void Dispose() {
            _levelDb.Dispose();
            _options.Dispose();
            _comparator.Dispose();
        }

        private IEnumerable<byte[]> GetBytesData() {
            using (var it = _levelDb.CreateIterator()) {
                for (it.SeekToFirst(); it.IsValid(); it.Next()) {
                    var piece = new StringPiece(it.Key());

                    KeyPrefix prefix;
                    ObjectStoreDataKey store;
                    if (!KeyPrefix.Decode(piece, out prefix) || prefix.Type != KeyType.ObjectStoreData ||
                            !ObjectStoreDataKey.Decode(piece.Reset(), out store)) continue;

                    var dataSlice = new StringPiece(it.Value());
                    long version;
                    if (!dataSlice.DecodeVarInt(out version)) continue;
                    yield return dataSlice.ToSwappedArray();
                }
            }
        }

        private static Dictionary<string, string> ParseBits(byte[] bits) {
            var result = new Dictionary<string, string>(4);
            using (var b = new BinaryReader(new MemoryStream(bits))) {
                // var header = b.ReadBytes(5);
                // Debug.WriteLine($"header: [{string.Join(", ", header.Select(x => $"0x{x:X}"))}]");

                string key = null;
                while (b.BaseStream.Position < b.BaseStream.Length - 1) {
                    byte b0 = b.ReadByte(), b1 = b.ReadByte();
                    if (b0 != 0x3f || b1 != 0x1) {
                        b.BaseStream.Seek(-2, SeekOrigin.Current);
                        break;
                    }

                    string value;

                    var t = b.ReadByte();
                    switch (t) {
                        case 0x53:
                            value = Encoding.UTF8.GetString(b.ReadBytes(b.ReadByte()));
                            Debug.WriteLine($"string: '{value}'");
                            break;

                        case 0x4E:
                            value = b.ReadDouble().ToString(CultureInfo.InvariantCulture);
                            Debug.WriteLine($"double: '{value}'");
                            break;

                        case 0x49:
                            long v;
                            if (!DecodeVarInt(b, out v)) {
                                throw new Exception("Damaged varint");
                            }

                            value = v.ToString(CultureInfo.InvariantCulture);
                            Debug.WriteLine($"varint: '{value}'");
                            break;

                        default:
                            throw new Exception($"Not supported type: 0x{t:X}");
                    }

                    if (key == null) {
                        key = value;
                    } else {
                        result[key] = value;
                        key = null;
                    }
                }

                Debug.WriteLine($"left: [{string.Join(", ", b.ReadBytes((int)(b.BaseStream.Length - b.BaseStream.Position)).Select(x => $"0x{x:X}"))}]");
            }

            return result;
        }

        private static bool DecodeVarInt(BinaryReader reader, out long result) {
            result = 0;
            var shift = 0;
            byte c;
            do {
                if (reader.BaseStream.Position == reader.BaseStream.Length) return false;
                c = reader.ReadByte();
                result |= (long)(c & 0x7f) << shift;
                shift += 7;
            } while ((c & 0x80) != 0);

            result /= 2; // ?
            return true;
        }

        public class StringPiece : IEnumerable<byte>, IComparable<StringPiece> {
            private readonly IReadOnlyList<byte> _array;
            private readonly int _start, _size;

            public int Position;

            public int Left => Position < _size ? _size - Position : 0;

            public StringPiece(NativeArray array) {
                _array = (NativeArray<byte>)array;
                _start = 0;
                _size = (int)array.ByteLength;
            }

            public StringPiece(IReadOnlyList<byte> array) {
                _array = array;
                _size = array.Count;
                _start = 0;
            }

            private StringPiece(IReadOnlyList<byte> array, int pos, int size) {
                _array = array;
                _size = size;
                _start = pos;
                Position = pos;
            }

            public StringPiece Fork() {
                return new StringPiece(_array, Position, Position + Left);
            }

            public StringPiece Reset() {
                return new StringPiece(_array, _start, _size);
            }

            public StringPiece Slice(int count) {
                var pos = Position;
                RemovePrefix(count);
                return new StringPiece(_array, pos, pos + count);
            }

            public void RemovePrefix(int count) {
                Position += count;
            }

            public byte Next() {
                return _array[Position++];
            }

            public byte[] Next(int count) {
                var result = new byte[count];
                for (var i = 0; i < count; i++) {
                    result[i] = _array[Position++];
                }
                return result;
            }

            public bool DecodeByte(out byte result) {
                if (Empty) {
                    result = 0;
                    return false;
                }

                result = _array[Position++];
                return true;
            }

            public bool DecodeInt(out long result) {
                if (Empty) {
                    result = 0;
                    return false;
                }

                result = 0;
                var shift = 0;
                while (!Empty) {
                    var c = Next();
                    result |= (long)c << shift;
                    shift += 8;
                }

                return true;
            }

            public bool DecodeVarInt(out long result) {
                if (Empty) {
                    result = 0;
                    return false;
                }

                result = 0;
                var shift = 0;
                byte c;
                do {
                    if (Empty) return false;
                    c = Next();
                    result |= (long)(c & 0x7f) << shift;
                    shift += 7;
                } while ((c & 0x80) != 0);
                return true;
            }

            public bool DecodeDouble(out double result) {
                if (Left < sizeof(double)) {
                    result = 0;
                    return false;
                }

                result = BitConverter.ToDouble(Next(sizeof(double)), 0);
                return true;
            }

            public bool DecodeStringWithLength(out string origin) {
                long length;
                if (!DecodeVarInt(out length) || length < 0) {
                    origin = null;
                    return false;
                }

                var bytes = length * 2;
                if (Left < bytes) {
                    origin = null;
                    return false;
                }

                origin = Slice((int)(length * 2)).ToString();
                return true;
            }

            public byte[] ToArray() {
                var count = Left;
                var pos = Position;
                var result = new byte[count];
                for (var i = 0; i < count; i++) {
                    result[i] = _array[pos++];
                }
                return result;
            }

            public byte[] ToSwappedArray() {
                var count = Left;
                var pos = Position;
                var result = new byte[count];
                for (var i = 0; i < count - 1; i += 2) {
                    result[i + 1] = _array[pos++];
                    result[i] = _array[pos++];
                }
                return result;
            }

            public int CompareTo(StringPiece other) {
                var a = Encoding.BigEndianUnicode.GetString(ToArray());
                var b = Encoding.BigEndianUnicode.GetString(other.ToArray());
                return string.Compare(a, b, StringComparison.Ordinal);
            }

            public override string ToString() {
                return Encoding.BigEndianUnicode.GetString(ToArray());
            }

            private class Enumerator : IEnumerator<byte> {
                private readonly IReadOnlyList<byte> _array;
                private readonly int _start;
                private readonly int _end;

                private int _pos;

                public Enumerator(IReadOnlyList<byte> array, int position, int left) {
                    _array = array;
                    _start = position;
                    _end = position + left;

                    _pos = _start - 1;
                }

                public void Dispose() { }

                public bool MoveNext() {
                    _pos++;
                    return _pos < _end;
                }

                public void Reset() {
                    _pos = _start - 1;
                }

                public byte Current => _pos < _end ? _array[_pos] : default(byte);

                object IEnumerator.Current => Current;
            }

            public IEnumerator<byte> GetEnumerator() {
                return new Enumerator(_array, Position, Left);
            }

            IEnumerator IEnumerable.GetEnumerator() {
                return GetEnumerator();
            }

            public bool Empty => Position >= _size;

            public void Skip(long length) {
                Position += (int)length;
            }
        }

        private static int CompareInts(long a, long b) {
            var diff = a - b;
            return diff < 0 ? -1 : (diff > 0 ? 1 : 0);
        }

        private static int CompareSizes(int a, int b) {
            return a > b ? 1 : (b > a ? -1 : 0);
        }

        public enum KeyType {
            GlobalMetadata,
            DatabaseMetadata,
            ObjectStoreData,
            ExistsEntry,
            IndexData,
            InvalidType,
            BlobEntry
        };

        public struct KeyPrefix : IComparable<KeyPrefix> {
            public long DatabaseId;
            public long ObjectStoreId;
            public long IndexId;

            public const byte KObjectStoreDataIndexId = 1;
            public const byte KExistsEntryIndexId = 2;
            public const byte KBlobEntryIndexId = 3;
            public const byte KMinimumIndexId = 30;

            public KeyType Type {
                get {
                    if (DatabaseId == 0) return KeyType.GlobalMetadata;
                    if (ObjectStoreId == 0) return KeyType.DatabaseMetadata;
                    if (IndexId == KObjectStoreDataIndexId) return KeyType.ObjectStoreData;
                    if (IndexId == KExistsEntryIndexId) return KeyType.ExistsEntry;
                    if (IndexId == KBlobEntryIndexId) return KeyType.BlobEntry;
                    if (IndexId >= KMinimumIndexId) return KeyType.IndexData;
                    return KeyType.InvalidType;
                }
            }

            public int CompareTo(KeyPrefix other) {
                if (DatabaseId != other.DatabaseId)
                    return CompareInts(DatabaseId, other.DatabaseId);
                if (ObjectStoreId != other.ObjectStoreId)
                    return CompareInts(ObjectStoreId, other.ObjectStoreId);
                if (IndexId != other.IndexId)
                    return CompareInts(IndexId, other.IndexId);
                return 0;
            }

            public override string ToString() {
                return $"(t: {Type}, d: {DatabaseId}, s: {ObjectStoreId}, i: {IndexId})";
            }

            public static bool Decode(StringPiece slice, out KeyPrefix prefix) {
                if (slice.Empty) {
                    prefix = default(KeyPrefix);
                    return false;
                }

                var firstByte = slice.Next();
                var databaseIdBytes = ((firstByte >> 5) & 0x7) + 1;
                var objectStoreIdBytes = ((firstByte >> 2) & 0x7) + 1;
                var indexIdBytes = (firstByte & 0x3) + 1;
                if (databaseIdBytes + objectStoreIdBytes + indexIdBytes > slice.Left) {
                    prefix = default(KeyPrefix);
                    return false;
                }

                prefix = new KeyPrefix();
                return slice.Slice(databaseIdBytes).DecodeInt(out prefix.DatabaseId) &&
                        slice.Slice(objectStoreIdBytes).DecodeInt(out prefix.ObjectStoreId) &&
                        slice.Slice(indexIdBytes).DecodeInt(out prefix.IndexId);
            }
        }

        public struct ObjectStoreMetaDataKey : IComparable<ObjectStoreMetaDataKey> {
            public long ObjectStoreId;
            public byte MetaDataTypeValue;

            public int CompareTo(ObjectStoreMetaDataKey other) {
                var x = CompareInts(ObjectStoreId, other.ObjectStoreId);
                return x != 0 ? x : MetaDataTypeValue - other.MetaDataTypeValue;
            }

            public static bool Decode(StringPiece slice, out ObjectStoreMetaDataKey result) {
                KeyPrefix prefix;
                if (!KeyPrefix.Decode(slice, out prefix)) {
                    result = default(ObjectStoreMetaDataKey);
                    return false;
                }

                if (slice.Empty) {
                    result = default(ObjectStoreMetaDataKey);
                    return false;
                }

                slice.Next();

                result = new ObjectStoreMetaDataKey();
                return slice.DecodeVarInt(out result.ObjectStoreId) && slice.DecodeByte(out result.MetaDataTypeValue);
            }

            public static int Compare(StringPiece a, StringPiece b) {
                ObjectStoreMetaDataKey keyA, keyB;
                return Decode(a, out keyA) && Decode(b, out keyB) ? keyA.CompareTo(keyB) : 0;
            }
        }

        private const byte KMaxSimpleGlobalMetaDataTypeByte = 5;
        private const byte KDatabaseFreeListTypeByte = 100;
        private const byte KDatabaseNameTypeByte = 201;

        private const byte KObjectStoreMetaDataTypeByte = 50;
        private const byte KIndexMetaDataTypeByte = 100;
        private const byte KObjectStoreFreeListTypeByte = 150;
        private const byte KIndexFreeListTypeByte = 151;
        private const byte KObjectStoreNamesTypeByte = 200;
        private const byte KIndexNamesKeyTypeByte = 201;

        enum MetaDataType {
            MaxSimpleMetadataType = 6
        };

        // https://cs.chromium.org/chromium/src/content/browser/indexed_db/indexed_db_database_callbacks.cc
        private static int Compare(StringPiece a, StringPiece b, bool onlyCompareIndexKeys) {
            var sliceA = a.Fork();
            var sliceB = b.Fork();

            KeyPrefix prefixA, prefixB;
            if (!KeyPrefix.Decode(sliceA, out prefixA) || !KeyPrefix.Decode(sliceB, out prefixB)) {
                return 0;
            }

            {
                var x = prefixA.CompareTo(prefixB);
                if (x != 0) return x;
            }

            switch (prefixA.Type) {
                case KeyType.GlobalMetadata: {
                        if (sliceA.Empty || sliceB.Empty) {
                            return 0;
                        }

                        var typeByteA = sliceA.Next();
                        var typeByteB = sliceB.Next();

                        {
                            var x = typeByteA - typeByteB;
                            if (x != 0) return x;
                        }

                        if (typeByteA < KMaxSimpleGlobalMetaDataTypeByte) {
                            return 0;
                        }

                        // Compare<> is used (which re-decodes the prefix) rather than an
                        // specialized CompareSuffix<> because metadata is relatively uncommon
                        // in the database.
                        switch (typeByteA) {
                            case KDatabaseFreeListTypeByte:
                                return DatabaseFreeListKey.Compare(a, b);

                            case KDatabaseNameTypeByte:
                                return DatabaseNameKey.Compare(a, b);
                        }
                        break;
                    }

                case KeyType.DatabaseMetadata: {
                        if (sliceA.Empty || sliceB.Empty) {
                            return 0;
                        }

                        var typeByteA = sliceA.Next();
                        var typeByteB = sliceB.Next();

                        {
                            var x = typeByteA - typeByteB;
                            if (x != 0) return x;
                        }

                        if (typeByteA < (int)MetaDataType.MaxSimpleMetadataType) {
                            return 0;
                        }

                        switch (typeByteA) {
                            case KObjectStoreMetaDataTypeByte:
                                return ObjectStoreMetaDataKey.Compare(a, b);

                            case KIndexMetaDataTypeByte:
                                return IndexMetaDataKey.Compare(a, b);

                            case KObjectStoreNamesTypeByte:
                                return ObjectStoreNamesKey.Compare(a, b);

                            case KObjectStoreFreeListTypeByte:
                                return ObjectStoreFreeListKey.Compare(a, b);

                            case KIndexFreeListTypeByte:
                                return IndexFreeListKey.Compare(a, b);

                            case KIndexNamesKeyTypeByte:
                                return IndexNamesKey.Compare(a, b);
                        }

                        break;
                    }

                case KeyType.ObjectStoreData:
                    return sliceA.Empty || sliceB.Empty ? CompareSizes(sliceA.Left, sliceB.Left) :
                            ObjectStoreDataKey.CompareSuffix(sliceA, sliceB);

                case KeyType.ExistsEntry:
                    return sliceA.Empty || sliceB.Empty ? CompareSizes(sliceA.Left, sliceB.Left) :
                            ExistsEntryKey.CompareSuffix(sliceA, sliceB);

                case KeyType.BlobEntry:
                    return sliceA.Empty || sliceB.Empty ? CompareSizes(sliceA.Left, sliceB.Left) :
                            BlobEntryKey.CompareSuffix(sliceA, sliceB);

                case KeyType.IndexData:
                    return sliceA.Empty || sliceB.Empty ? CompareSizes(sliceA.Left, sliceB.Left) :
                            IndexDataKey.CompareSuffix(sliceA, sliceB, onlyCompareIndexKeys);

                case KeyType.InvalidType:
                    break;
            }

            return 0;
        }


        public const byte KIndexedDbKeyNullTypeByte = 0;
        public const byte KIndexedDbKeyStringTypeByte = 1;
        public const byte KIndexedDbKeyDateTypeByte = 2;
        public const byte KIndexedDbKeyNumberTypeByte = 3;
        public const byte KIndexedDbKeyArrayTypeByte = 4;
        public const byte KIndexedDbKeyMinKeyTypeByte = 5;
        public const byte KIndexedDbKeyBinaryTypeByte = 6;

        private static bool ConsumeEncodedIdbKey(StringPiece slice) {
            var type = slice.Next();

            switch (type) {
                case KIndexedDbKeyNullTypeByte:
                case KIndexedDbKeyMinKeyTypeByte:
                    return true;
                case KIndexedDbKeyArrayTypeByte: {
                        long length;
                        if (!slice.DecodeVarInt(out length)) return false;
                        while (length-- != 0) {
                            if (!ConsumeEncodedIdbKey(slice)) return false;
                        }
                        return true;
                    }
                case KIndexedDbKeyBinaryTypeByte: {
                        long length;
                        if (!slice.DecodeVarInt(out length) || length < 0) return false;
                        if (slice.Left < length) return false;
                        slice.Skip(length);
                        return true;
                    }
                case KIndexedDbKeyStringTypeByte: {
                        long length;
                        if (!slice.DecodeVarInt(out length) || length < 0) return false;
                        if (slice.Left < length * 2) return false;
                        slice.Skip(length * 2);
                        return true;
                    }
                case KIndexedDbKeyDateTypeByte:
                case KIndexedDbKeyNumberTypeByte:
                    if (slice.Left < sizeof(double)) return false;
                    slice.Skip(sizeof(double));
                    return true;
            }

            return false;
        }

        private enum WebIdbKeyType {
            WebIdbKeyTypeInvalid = 0,
            WebIdbKeyTypeArray = 1,
            WebIdbKeyTypeBinary = 2,
            WebIdbKeyTypeString = 3,
            WebIdbKeyTypeDate = 4,
            WebIdbKeyTypeNumber = 5,
            WebIdbKeyTypeMin = 7,
        };

        private static WebIdbKeyType KeyTypeByteToKeyType(byte type) {
            switch (type) {
                case KIndexedDbKeyNullTypeByte:
                    return WebIdbKeyType.WebIdbKeyTypeInvalid;
                case KIndexedDbKeyArrayTypeByte:
                    return WebIdbKeyType.WebIdbKeyTypeArray;
                case KIndexedDbKeyBinaryTypeByte:
                    return WebIdbKeyType.WebIdbKeyTypeBinary;
                case KIndexedDbKeyStringTypeByte:
                    return WebIdbKeyType.WebIdbKeyTypeString;
                case KIndexedDbKeyDateTypeByte:
                    return WebIdbKeyType.WebIdbKeyTypeDate;
                case KIndexedDbKeyNumberTypeByte:
                    return WebIdbKeyType.WebIdbKeyTypeNumber;
                case KIndexedDbKeyMinKeyTypeByte:
                    return WebIdbKeyType.WebIdbKeyTypeMin;
            }

            return WebIdbKeyType.WebIdbKeyTypeInvalid;
        }

        private static int CompareTypes(WebIdbKeyType a, WebIdbKeyType b) { return b - a; }

        private static int CompareEncodedBinary(StringPiece slice1, StringPiece slice2, out bool ok) {
            long len1, len2;
            if (!slice1.DecodeVarInt(out len1) || !slice2.DecodeVarInt(out len2) || len1 < 0 || len2 < 0) {
                ok = false;
                return 0;
            }

            var size1 = (int)len1;
            var size2 = (int)len2;
            if (slice1.Left < size1 || slice2.Left < size2) {
                ok = false;
                return 0;
            }

            // Extract the binary data, and advance the passed slices.
            ok = true;
            return slice1.Slice(size1).CompareTo(slice2.Slice(size2));
        }

        private static int CompareEncodedStringsWithLength(StringPiece slice1, StringPiece slice2, out bool ok) {
            long len1, len2;
            if (!slice1.DecodeVarInt(out len1) || !slice2.DecodeVarInt(out len2) || len1 < 0 || len2 < 0) {
                ok = false;
                return 0;
            }

            var size1 = (int)len1 * 2;
            var size2 = (int)len2 * 2;
            if (slice1.Left < size1 || slice2.Left < size2) {
                ok = false;
                return 0;
            }

            // Extract the binary data, and advance the passed slices.
            ok = true;
            return slice1.Slice(size1).CompareTo(slice2.Slice(size2));
        }

        private static int CompareEncodedIdbKeys(StringPiece sliceA, StringPiece sliceB, out bool ok) {
            ok = true;
            var typeA = sliceA.Next();
            var typeB = sliceB.Next();

            {
                var x = CompareTypes(KeyTypeByteToKeyType(typeA), KeyTypeByteToKeyType(typeB));
                if (x != 0) return x;
            }

            switch (typeA) {
                case KIndexedDbKeyNullTypeByte:
                case KIndexedDbKeyMinKeyTypeByte:
                    // Null type or max type; no payload to compare.
                    return 0;
                case KIndexedDbKeyArrayTypeByte: {
                        long lengthA, lengthB;
                        if (!sliceA.DecodeVarInt(out lengthA) || !sliceB.DecodeVarInt(out lengthB)) {
                            ok = false;
                            return 0;
                        }

                        for (long i = 0; i < lengthA && i < lengthB; ++i) {
                            var result = CompareEncodedIdbKeys(sliceA, sliceB, out ok);
                            if (!ok || result != 0) return result;
                        }

                        return (int)(lengthA - lengthB);
                    }
                case KIndexedDbKeyBinaryTypeByte:
                    return CompareEncodedBinary(sliceA, sliceB, out ok);
                case KIndexedDbKeyStringTypeByte:
                    return CompareEncodedStringsWithLength(sliceA, sliceB, out ok);
                case KIndexedDbKeyDateTypeByte:
                case KIndexedDbKeyNumberTypeByte: {
                        double d, e;
                        if (!sliceA.DecodeDouble(out d) || !sliceB.DecodeDouble(out e)) {
                            ok = false;
                            return 0;
                        }
                        return d < e ? -1 : (d > e ? 1 : 0);
                    }
            }

            return 0;
        }

        private static bool ExtractEncodedIdbKey(StringPiece slice, out string result) {
            var start = slice.Fork();
            if (!ConsumeEncodedIdbKey(slice)) {
                result = null;
                return false;
            }

            result = start.Slice(start.Left - slice.Left).ToString();
            return true;
        }

        public struct ObjectStoreDataKey {
            public string EncodedUserKey;

            public static bool Decode(StringPiece slice, out ObjectStoreDataKey result) {
                KeyPrefix prefix;
                if (!KeyPrefix.Decode(slice, out prefix)) {
                    result = default(ObjectStoreDataKey);
                    return false;
                }

                result = new ObjectStoreDataKey();
                if (!ExtractEncodedIdbKey(slice, out result.EncodedUserKey)) {
                    return false;
                }

                return true;
            }

            public static int CompareSuffix(StringPiece sliceA, StringPiece sliceB) {
                bool ok;
                return CompareEncodedIdbKeys(sliceA, sliceB, out ok);
            }

            public override string ToString() {
                return $"{GetType().Name}(encoded_user_key={EncodedUserKey})";
            }
        }

        public struct ExistsEntryKey {
            public static int CompareSuffix(StringPiece sliceA, StringPiece sliceB) {
                bool ok;
                return CompareEncodedIdbKeys(sliceA, sliceB, out ok);
            }
        }

        public struct IndexMetaDataKey : IComparable<IndexMetaDataKey> {
            public long ObjectStoreId;
            public long IndexId;
            public byte MetaDataTypeValue;

            public static bool Decode(StringPiece slice, out IndexMetaDataKey result) {
                KeyPrefix prefix;
                byte typeByte;
                if (!KeyPrefix.Decode(slice, out prefix) || !slice.DecodeByte(out typeByte)) {
                    result = default(IndexMetaDataKey);
                    return false;
                }

                result = new IndexMetaDataKey();
                return slice.DecodeVarInt(out result.ObjectStoreId) &&
                        slice.DecodeVarInt(out result.IndexId) &&
                        slice.DecodeByte(out result.MetaDataTypeValue);
            }

            public static int Compare(StringPiece a, StringPiece b) {
                IndexMetaDataKey keyA, keyB;
                return Decode(a, out keyA) && Decode(b, out keyB) ? keyA.CompareTo(keyB) : 0;
            }

            public int CompareTo(IndexMetaDataKey other) {
                var x = CompareInts(ObjectStoreId, other.ObjectStoreId);
                if (x != 0) return x;

                x = CompareInts(IndexId, other.IndexId);
                if (x != 0) return x;

                return MetaDataTypeValue - other.MetaDataTypeValue;
            }
        }

        public struct DatabaseNameKey : IComparable<DatabaseNameKey> {
            public string Origin;
            public string DatabaseName;

            public static bool Decode(StringPiece slice, out DatabaseNameKey result) {
                KeyPrefix prefix;
                byte typeByte;
                if (!KeyPrefix.Decode(slice, out prefix) || !slice.DecodeByte(out typeByte)) {
                    result = default(DatabaseNameKey);
                    return false;
                }

                result = new DatabaseNameKey();
                return slice.DecodeStringWithLength(out result.Origin) && slice.DecodeStringWithLength(out result.DatabaseName);
            }

            public static int Compare(StringPiece a, StringPiece b) {
                DatabaseNameKey keyA, keyB;
                return Decode(a, out keyA) && Decode(b, out keyB) ? keyA.CompareTo(keyB) : 0;
            }

            public int CompareTo(DatabaseNameKey other) {
                var x = string.Compare(Origin, other.Origin, StringComparison.Ordinal);
                return x != 0 ? x : string.Compare(DatabaseName, other.DatabaseName, StringComparison.Ordinal);
            }

            public override string ToString() {
                return $"{GetType().Name}({Origin}, {DatabaseName})";
            }
        }

        public struct ObjectStoreFreeListKey : IComparable<ObjectStoreFreeListKey> {
            public long ObjectStoreId;

            public static bool Decode(StringPiece slice, out ObjectStoreFreeListKey result) {
                KeyPrefix prefix;
                byte typeByte;
                if (!KeyPrefix.Decode(slice, out prefix) || !slice.DecodeByte(out typeByte)) {
                    result = default(ObjectStoreFreeListKey);
                    return false;
                }

                result = new ObjectStoreFreeListKey();
                return slice.DecodeVarInt(out result.ObjectStoreId);
            }

            public static int Compare(StringPiece a, StringPiece b) {
                ObjectStoreFreeListKey keyA, keyB;
                return Decode(a, out keyA) && Decode(b, out keyB) ? keyA.CompareTo(keyB) : 0;
            }

            public int CompareTo(ObjectStoreFreeListKey other) {
                return CompareInts(ObjectStoreId, other.ObjectStoreId);
            }

            public override string ToString() {
                return $"{GetType().Name}({ObjectStoreId})";
            }
        }

        public struct IndexFreeListKey : IComparable<IndexFreeListKey> {
            public long ObjectStoreId;
            public long IndexId;

            public static bool Decode(StringPiece slice, out IndexFreeListKey result) {
                KeyPrefix prefix;
                byte typeByte;
                if (!KeyPrefix.Decode(slice, out prefix) || !slice.DecodeByte(out typeByte)) {
                    result = default(IndexFreeListKey);
                    return false;
                }

                result = new IndexFreeListKey();
                return slice.DecodeVarInt(out result.ObjectStoreId) && slice.DecodeVarInt(out result.IndexId);
            }

            public static int Compare(StringPiece a, StringPiece b) {
                IndexFreeListKey keyA, keyB;
                return Decode(a, out keyA) && Decode(b, out keyB) ? keyA.CompareTo(keyB) : 0;
            }

            public int CompareTo(IndexFreeListKey other) {
                var x = CompareInts(ObjectStoreId, other.ObjectStoreId);
                return x != 0 ? x : CompareInts(IndexId, other.IndexId);
            }

            public override string ToString() {
                return $"{GetType().Name}(object_store_id={ObjectStoreId}, index_id={IndexId})";
            }
        }

        public struct IndexNamesKey : IComparable<IndexNamesKey> {
            public long ObjectStoreId;
            public string IndexName;

            public static bool Decode(StringPiece slice, out IndexNamesKey result) {
                KeyPrefix prefix;
                byte typeByte;
                if (!KeyPrefix.Decode(slice, out prefix) || !slice.DecodeByte(out typeByte)) {
                    result = default(IndexNamesKey);
                    return false;
                }

                result = new IndexNamesKey();
                return slice.DecodeVarInt(out result.ObjectStoreId) && slice.DecodeStringWithLength(out result.IndexName);
            }

            public static int Compare(StringPiece a, StringPiece b) {
                IndexNamesKey keyA, keyB;
                return Decode(a, out keyA) && Decode(b, out keyB) ? keyA.CompareTo(keyB) : 0;
            }

            public int CompareTo(IndexNamesKey other) {
                var x = CompareInts(ObjectStoreId, other.ObjectStoreId);
                return x != 0 ? x : string.Compare(IndexName, other.IndexName, StringComparison.Ordinal);
            }

            public override string ToString() {
                return $"{GetType().Name}(object_store_id={ObjectStoreId}, index_name={IndexName})";
            }
        }

        public struct DatabaseFreeListKey : IComparable<DatabaseFreeListKey> {
            public long DatabaseId;

            public static bool Decode(StringPiece slice, out DatabaseFreeListKey result) {
                KeyPrefix prefix;
                byte typeByte;
                if (!KeyPrefix.Decode(slice, out prefix) || !slice.DecodeByte(out typeByte)) {
                    result = default(DatabaseFreeListKey);
                    return false;
                }

                result = new DatabaseFreeListKey();
                return slice.DecodeVarInt(out result.DatabaseId);
            }

            public static int Compare(StringPiece a, StringPiece b) {
                DatabaseFreeListKey keyA, keyB;
                return Decode(a, out keyA) && Decode(b, out keyB) ? keyA.CompareTo(keyB) : 0;
            }

            public int CompareTo(DatabaseFreeListKey other) {
                return CompareInts(DatabaseId, other.DatabaseId);
            }
        }

        public struct ObjectStoreNamesKey : IComparable<ObjectStoreNamesKey> {
            private string _objectStoreName;

            public static bool Decode(StringPiece slice, out ObjectStoreNamesKey result) {
                KeyPrefix prefix;
                byte typeByte;
                if (!KeyPrefix.Decode(slice, out prefix) || !slice.DecodeByte(out typeByte)) {
                    result = default(ObjectStoreNamesKey);
                    return false;
                }

                result = new ObjectStoreNamesKey();
                return slice.DecodeStringWithLength(out result._objectStoreName);
            }

            public static int Compare(StringPiece a, StringPiece b) {
                ObjectStoreNamesKey keyA, keyB;
                return Decode(a, out keyA) && Decode(b, out keyB) ? keyA.CompareTo(keyB) : 0;
            }

            public int CompareTo(ObjectStoreNamesKey other) {
                return string.Compare(_objectStoreName, other._objectStoreName, StringComparison.Ordinal);
            }

            public override string ToString() {
                return $"{GetType().Name}({_objectStoreName})";
            }
        }

        public struct BlobEntryKey {
            public static int CompareSuffix(StringPiece sliceA, StringPiece sliceB) {
                bool ok;
                return CompareEncodedIdbKeys(sliceA, sliceB, out ok);
            }
        }

        public struct IndexDataKey {
            public static int CompareSuffix(StringPiece sliceA, StringPiece sliceB, bool onlyCompareIndexKeys) {
                bool ok;
                int result = CompareEncodedIdbKeys(sliceA, sliceB, out ok);
                if (!ok || result != 0 || onlyCompareIndexKeys) return result;

                // sequence number [optional]
                long sequenceNumberA = -1;
                long sequenceNumberB = -1;
                if (!sliceA.Empty && !sliceA.DecodeVarInt(out sequenceNumberA) ||
                        !sliceB.Empty && !sliceB.DecodeVarInt(out sequenceNumberB)) {
                    return 0;
                }

                if (sliceA.Empty || sliceB.Empty) {
                    return CompareSizes(sliceA.Left, sliceB.Left);
                }

                // primary key [optional]
                result = CompareEncodedIdbKeys(sliceA, sliceB, out ok);
                if (!ok || result != 0) return result;

                return CompareInts(sequenceNumberA, sequenceNumberB);
            }
        }
    }
}