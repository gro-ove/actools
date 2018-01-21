using System;
using System.Collections.Generic;
using System.Linq;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcTools.DataAnalyzer {
    internal class HashStorageObsoleteException : Exception {
        public HashStorageObsoleteException(string msg) : base(msg) { }
    }

    public class HashStorage {
        private static readonly int Version = 1;

        internal int ParamsHashCode;

        private readonly string[] _setsKeys;
        private readonly Dictionary<string, byte[][]> _dictionary;

        public int Count => _dictionary.Count;

        public HashStorage(string[] keys) {
            _setsKeys = keys;
            _dictionary = new Dictionary<string, byte[][]>();
        }

        private HashStorage([NotNull] string[] keys, [NotNull] string filename) : this(keys) {
            using (var reader = new ReadAheadBinaryReader(filename)) {
                if (reader.ReadByte() != 0x8a || reader.ReadByte() != 0x56) {
                    throw new Exception("Invalid format");
                }

                if (reader.ReadInt32() != Version) {
                    throw new Exception("Invalid version");
                }

                ParamsHashCode = reader.ReadInt32();

                var keysAmount = reader.ReadInt32();
                if (keysAmount != _setsKeys.Length) {
                    throw new HashStorageObsoleteException("Incompatible keys");
                }

                var sizePerSet = new int[keysAmount];
                for (var i = 0; i < keysAmount; i++) {
                    if (reader.ReadInt32() != _setsKeys[i].GetHashCode()) {
                        throw new HashStorageObsoleteException("Incompatible keys");
                    }

                    sizePerSet[i] = reader.ReadInt32();
                }

                var amount = reader.ReadInt32();
                for (var i = 0; i < amount; i++) {
                    var carId = reader.ReadString();

                    var data = new byte[keysAmount][];
                    for (var j = 0; j < keysAmount; j++) {
                        data[j] = reader.ReadBytes(sizePerSet[j]);
                    }

                    _dictionary[carId] = data;
                }
            }
        }

        public void Add(string carId, [NotNull] byte[][] hashValues) {
            _dictionary[carId] = hashValues;
        }

        public void SaveTo([NotNull] string filename) {
            using (var writer = new ExtendedBinaryWriter(filename)) {
                writer.Write((byte)0x8a);
                writer.Write((byte)0x56);
                writer.Write(Version);
                writer.Write(ParamsHashCode);

                var first = _dictionary.Values.FirstOrDefault();
                writer.Write(_setsKeys.Length);
                for (var i = 0; i < _setsKeys.Length; i++) {
                    writer.Write(_setsKeys[i].GetHashCode());
                    writer.Write(first?[i].Length ?? 0);
                }

                writer.Write(_dictionary.Count);
                foreach (var pair in _dictionary) {
                    writer.Write(pair.Key);
                    foreach (var b in pair.Value) {
                        writer.Write(b);
                    }
                }
            }
        }

        public IEnumerable<Simular> FindSimular([NotNull] string carId, [NotNull] string setId, [NotNull] byte[] hashValues, double threshold,
                [NotNull] RulesSet set, bool keepWorkedRules) {
            var index = _setsKeys.IndexOf(setId);
            if (index < 0) {
                throw new Exception($"Set “{setId}” is not defined");
            }

            foreach (var pair in _dictionary) {
                if (pair.Key == carId) continue;

                var pairHash = pair.Value[index];
                var value = RulesSet.CompareHashes(hashValues, pairHash, set, keepWorkedRules, out var workedRules);
                if (value > threshold) {
                    yield return new Simular { CarId = pair.Key, Value = value, WorkedRules = workedRules };
                }
            }
        }

        public class Simular {
            public string CarId;
            public double Value;
            public RulesSet.Rule[] WorkedRules;
        }

        public static HashStorage FromFile([NotNull] string[] setsKeys, [NotNull] string filename) {
            return new HashStorage(setsKeys, filename);
        }

        public bool HasCar([NotNull] string carId) {
            return _dictionary.ContainsKey(carId);
        }
    }
}