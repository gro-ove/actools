using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AcTools.DataAnalyzer {
    public class HashStorage {
        private readonly Dictionary<string, Dictionary<string, string>> _dictionary;

        public HashStorage() {
            _dictionary = new Dictionary<string, Dictionary<string, string>>();
        }

        private HashStorage(params string[] filenames)
            : this() {

            foreach (var filename in filenames) {
                foreach (var line in File.ReadAllLines(filename)) {
                    var split = line.Split(new []{ ':' }, 2);
                    var carId = split[0];
                    var entries = split[1].Split(',');

                    foreach (var temp in entries.Select(entry => entry.Split(new[] {'='}, 2))) {
                        Add(carId, temp[0], temp[1]);
                    }
                }
            }
        }

        public void Add(string carId, string rulesSetId, string hashValue) {
            if (!_dictionary.ContainsKey(rulesSetId)) {
                _dictionary[rulesSetId] = new Dictionary<string, string>();
            }

            _dictionary[rulesSetId][carId] = hashValue;
        }

        public IEnumerable<Simular> FindSimular(string carId, string rulesSetId, string hashValue, double threshold, RulesSet set = null) {
            RulesSet.Rule[] workedRules = null;

            if (!_dictionary.ContainsKey(rulesSetId)) yield break;

            foreach (var pair in _dictionary[rulesSetId]) {
                if (pair.Key == carId) continue;

                var value = set == null ? RulesSet.CompareHashes(hashValue, pair.Value) : RulesSet.CompareHashes(hashValue, pair.Value, set, out workedRules);
                if (value > threshold) {
                    yield return new Simular {CarId = pair.Key, Value = value, WorkedRules = workedRules};
                }
            }
        }

        public class Simular {
            public string CarId;
            public double Value;
            public RulesSet.Rule[] WorkedRules;
        }

        public static HashStorage FromFile(params string[] filenames) {
            return new HashStorage(filenames);
        }

        public bool HasCar(string carId) {
            return _dictionary.Values.Any(rules => rules.ContainsKey(carId));
        }
    }
}