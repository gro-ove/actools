using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AcTools.DataFile;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using JetBrains.Annotations;

namespace AcTools.DataAnalyzer {
    public class RulesWrapper {
        private class RulesEntry {
            public readonly string Id;
            public readonly string CommonId;
            public readonly RulesSet Rules;

            public RulesEntry(string id, string commonId, RulesSet rules) {
                Id = id;
                CommonId = commonId;
                Rules = rules;
            }

            public static RulesEntry Create(KeyValuePair<string, string> x) {
                return new RulesEntry(x.Key, x.Key.Split(':')[0], RulesSet.FromText(x.Value));
            }

            protected bool Equals(RulesEntry other) {
                return string.Equals(Id, other.Id) && Equals(Rules, other.Rules);
            }

            public override bool Equals(object obj) {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != this.GetType()) return false;
                return Equals((RulesEntry)obj);
            }

            public override int GetHashCode() {
                unchecked {
                    return ((Id?.GetHashCode() ?? 0) * 397) ^ (Rules?.GetHashCode() ?? 0);
                }
            }
        }

        private readonly string _acRoot;
        private readonly RulesEntry[] _rules;
        private readonly string[] _rulesKeys;
        private readonly string _storageLocation;
        private readonly string[] _donorIds;
        private readonly int _paramsHashCode;

        [CanBeNull]
        private HashStorage _hashStorage;

        public RulesWrapper(string acRoot, string rules, string storageLocation, string[] donorIds) {
            _acRoot = acRoot;
            _rules = TagFile.FromData(rules).Select(RulesEntry.Create).ToArray();
            _rulesKeys = _rules.Select(x => x.Id).ToArray();
            _storageLocation = storageLocation;
            _donorIds = donorIds;
            _paramsHashCode = acRoot.GetHashCode() ^ _rules.GetEnumerableHashCode() ^ donorIds.GetEnumerableHashCode();

            try {
                _hashStorage = File.Exists(storageLocation) ? HashStorage.FromFile(_rulesKeys, storageLocation) : new HashStorage(_rulesKeys);
            } catch (HashStorageObsoleteException) {
                _hashStorage = null;
            } catch (Exception e) {
                AcToolsLogging.Write(e);
                _hashStorage = null;
            }
        }

        private HashStorage CreateNew([NotNull] RulesEntry[] rulesSet, string[] carIds) {
            var hashStorage = new HashStorage(_rulesKeys);
            foreach (var car in carIds) {
                var carLocation = FileUtils.GetCarDirectory(_acRoot, car);
                if (!Directory.Exists(carLocation)) continue;

                var carData = DataWrapper.FromCarDirectory(carLocation);
                var bytes = new byte[rulesSet.Length][];
                for (var i = 0; i < rulesSet.Length; i++) {
                    var set = rulesSet[i];
                    bytes[i] = set.Rules.GetHash(carData);
                }

                hashStorage.Add(car, bytes);
            }

            hashStorage.ParamsHashCode = _paramsHashCode;
            hashStorage.SaveTo(_storageLocation);
            return hashStorage;
        }

        public async Task EnsureActualAsync() {
            if (_hashStorage == null || _paramsHashCode != _hashStorage.ParamsHashCode) {
                _hashStorage = await Task.Run(() => CreateNew(_rules, _donorIds));
            }
        }

        public void EnsureActual() {
            if (_hashStorage == null || _paramsHashCode != _hashStorage.ParamsHashCode) {
                var w = Stopwatch.StartNew();
                _hashStorage = CreateNew(_rules, _donorIds);
                AcToolsLogging.Write($"Update storage: {w.Elapsed.TotalMilliseconds:F2} ms");
            }
        }

        public static RulesWrapper FromFile(string acRoot, string rulesFilename, string storageLocation, string[] donorIds) {
            return new RulesWrapper(acRoot, File.ReadAllText(rulesFilename), storageLocation, donorIds);
        }

        private RulesEntry GetSet(string setId) {
            for (var i = 0; i < _rules.Length; i++) {
                var rule = _rules[i];
                if (rule.Id == setId) return rule;
            }

            for (var i = 0; i < _rules.Length; i++) {
                var rule = _rules[i];
                if (rule.CommonId == setId) return rule;
            }

            return null;
        }

        public IEnumerable<HashStorage.Simular> FindSimular(string carId, string setId, bool keepWorkedRules, double threshold) {
            var carLocation = FileUtils.GetCarDirectory(_acRoot, carId);
            return Directory.Exists(carLocation) ?
                    FindSimular(DataWrapper.FromCarDirectory(carLocation), setId, keepWorkedRules, threshold) :
                    new HashStorage.Simular[0];
        }

        public IEnumerable<HashStorage.Simular> FindSimular(DataWrapper carData, string setId, bool keepWorkedRules, double threshold) {
            var hashStorage = _hashStorage;
            if (hashStorage == null) return new HashStorage.Simular[0];
            
            var entry = GetSet(setId);
            if (entry == null) {
                AcToolsLogging.Write("Rules not found: " + setId);
                return new HashStorage.Simular[0];
            }

            var hashValue = entry.Rules.GetHash(carData);
            return hashStorage.FindSimular(Path.GetFileName(carData.ParentDirectory) ?? "", entry.Id, hashValue, threshold,
                    entry.Rules, keepWorkedRules);
        }
    }
}