using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcTools.DataFile;
using AcTools.NeuralTyres.Data;
using AcTools.NeuralTyres.Implementations;
using AcTools.Utils.Helpers;
using AcTools.Utils.Physics;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcTools.NeuralTyres {
    public class TyresMachine {
        [NotNull]
        private readonly NeuralTyresOptions _options;

        [NotNull]
        private readonly Dictionary<string, INeuralNetwork> _networks = new Dictionary<string, INeuralNetwork>();

        [NotNull]
        private readonly NeuralTyresSource[] _tyresSources;

        [NotNull]
        public IReadOnlyList<NeuralTyresSource> Sources => _tyresSources;

        [NotNull]
        private readonly string[] _outputKeys;

        [NotNull]
        public IReadOnlyList<string> OutputKeys => _outputKeys;

        [NotNull]
        private readonly Normalization[] _inputNormalizations, _outputNormalizations;

        [CanBeNull]
        private readonly double[][] _inputNormalized, _outputNormalized;

        private readonly IReadOnlyDictionary<string, Lut>[] _luts;

        public int TyresVersion { get; }

        private TyresMachine([NotNull] NeuralTyresEntry[] tyres, [NotNull] NeuralTyresOptions options) {
            if (tyres == null) throw new ArgumentNullException(nameof(tyres));
            if (tyres.Length == 0) throw new ArgumentException("Value cannot be an empty collection.", nameof(tyres));
            _options = options ?? throw new ArgumentNullException(nameof(options));

            // Some details to identify Machine later if needed
            _tyresSources = tyres.Cast<NeuralTyresSource>().ToArray();

            // Tyres version
            TyresVersion = tyres[0].Version;
            if (tyres.Any(x => x.Version != TyresVersion)) {
                throw new ArgumentException("Inconsistent versions");
            }

            // LUTs, just in case
            _luts = tyres.Select(x => x.Luts).ToArray();

            // Input normalizations and values
            _inputNormalized = new double[tyres.Length][];
            for (var i = 0; i < tyres.Length; i++) {
                _inputNormalized[i] = new double[3];
            }

            _inputNormalizations = new Normalization[options.InputKeys.Length];
            for (var i = 0; i < _inputNormalizations.Length; i++) {
                _inputNormalizations[i] = Normalization.BuildNormalization(tyres, options.InputKeys[i], options.ValuePadding, out var normalized);
                for (var j = 0; j < normalized.Length; j++) {
                    _inputNormalized[j][i] = normalized[j];
                }
            }

            // Output normalizations and values
            _outputKeys = tyres[0].Keys.ApartFrom(options.IgnoredKeys).ToArray();
            _outputNormalizations = new Normalization[_outputKeys.Length];
            _outputNormalized = new double[_outputKeys.Length][];

            for (var i = 0; i < _outputKeys.Length; i++) {
                _outputNormalizations[i] = Normalization.BuildNormalization(tyres, _outputKeys[i], options.ValuePadding,
                        out _outputNormalized[i]);
            }
        }

        private void Train([CanBeNull] IProgress<Tuple<string, double?>> progress, CancellationToken cancellationToken) {
            var inputs = _inputNormalized;
            var outputs = _outputNormalized;
            if (inputs == null || outputs == null) {
                throw new Exception("This instance can’t be trained");
            }

            var keys = _outputKeys;
            double processed = 0, total = keys.Length;
            Parallel.ForEach(keys.Select((x, i) => new {
                Key = x,
                Outputs = outputs[i].Select(y => new[]{ y }).ToArray()
            }), value => {
                if (cancellationToken.IsCancellationRequested) return;

                var network = new AverageNetwork<FannNetwork>();
                lock (_networks) {
                    _networks[value.Key] = network;
                }

                network.SetOptions(_options);
                network.Train(inputs, value.Outputs);

                Console.WriteLine($"Trained: {value.Key}");
                progress?.Report(Tuple.Create<string, double?>(value.Key, ++processed / total));
            });
        }

        private readonly int SaveFormatVersion = 1;

        private TyresMachine(string filename) {
            using (var stream = File.OpenRead(filename))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, true)) {
                var manifest = Read<JObject>("Manifest.json");
                if (manifest.GetIntValueOnly("version") != SaveFormatVersion) {
                    throw new Exception("Unsupported version: " + manifest.GetIntValueOnly("version"));
                }

                TyresVersion = manifest.GetIntValueOnly("tyresVersion", 0);
                if (TyresVersion < 7) {
                    throw new Exception("Unsupported tyres version: " + TyresVersion);
                }

                _options = Read<NeuralTyresOptions>("Options.json");
                _tyresSources = Read<NeuralTyresSource[]>("Input/Sources.json");
                _luts = Read<Dictionary<string, string>[]>("Input/LUTs.json")
                        .Select(x => (IReadOnlyDictionary<string, Lut>)x.ToDictionary(y => y.Key, y => Lut.FromValue(y.Value))).ToArray();
                _inputNormalizations = Read<Normalization[]>("Input/Normalizations.json");
                _outputKeys = Read<string[]>("Output/Keys.json");
                _outputNormalizations = Read<Normalization[]>("Output/Normalizations.json");

                foreach (var key in _outputKeys) {
                    var typeName = zip.ReadString($"Networks/{key}/Type.txt");
                    var type = Type.GetType(typeName);
                    if (type == null) {
                        throw new Exception("Type not found: " + typeName);
                    }

                    var instance = (INeuralNetwork)Activator.CreateInstance(type);
                    instance.SetOptions(_options);
                    instance.Load(zip.ReadBytes($"Networks/{key}/Data.bin"));
                    _networks[key] = instance;
                }

                T Read<T>(string key) {
                    return JsonConvert.DeserializeObject<T>(zip.ReadString(key));
                }
            }
        }

        public void Save(string filename) {
            using (var stream = File.Create(filename))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true)) {
                Add("Manifest.json", new JObject {
                    ["version"] = SaveFormatVersion,
                    ["tyresVersion"] = TyresVersion
                });

                Add("Options.json", _options);
                Add("Input/Sources.json", _tyresSources);
                Add("Input/LUTs.json", _luts.Select(x => x.Where(y => y.Value != null).ToDictionary(y => y.Key, y => y.Value.ToString())));
                Add("Input/Normalizations.json", _inputNormalizations);
                Add("Output/Keys.json", _outputKeys);
                Add("Output/Normalizations.json", _outputNormalizations);
                foreach (var network in _networks) {
                    var networkType = network.Value.GetType().FullName;
                    var data = network.Value.Save();
                    if (networkType == null || data == null) continue;
                    zip.AddString($"Networks/{network.Key}/Type.txt", networkType);
                    zip.AddBytes($"Networks/{network.Key}/Data.bin", data);
                }

                void Add(string key, object data) {
                    zip.AddString(key, JsonConvert.SerializeObject(data, Formatting.Indented));
                }
            }
        }

        public Normalization GetNormalization(string key) {
            return _inputNormalizations.ElementAtOrDefault(_options.InputKeys.IndexOf(key));
        }

        private void SetInputs(double[] input, NeuralTyresEntry result) {
            if (input.Length != _options.InputKeys.Length) {
                throw new Exception("Input keys and inputs lengths don’t match");
            }

            for (var i = 0; i < _options.InputKeys.Length; i++) {
                var key = _options.InputKeys[i];
                var normalization = _inputNormalizations[i];
                if (!normalization.Fits(input[i])) {
                    throw new Exception($"Value  {input[i]} for {key} is out of range {normalization}");
                }

                if (!IsProceduralValue(key)) {
                    SetValue(result, key, input[i]);
                    input[i] = normalization.Normalize(input[i]);
                }
            }

            for (var i = 0; i < _options.InputKeys.Length; i++) {
                var key = _options.InputKeys[i];
                if (IsProceduralValue(key)) {
                    SetValue(result, key, input[i]);
                    input[i] = _inputNormalizations[i].Normalize(input[i]);
                }
            }
        }

        public NeuralTyresEntry Conjure(params double[] input) {
            var result = new NeuralTyresEntry();
            SetInputs(input, result);

            foreach (var n in _networks) {
                var normalization = _outputNormalizations[_outputKeys.IndexOf(n.Key)];
                SetValue(result, n.Key, normalization.Denormalize(n.Value.Compute(input)));
            }

            return result;
        }

        public double Conjure(string outputKey, params double[] input) {
            var result = new NeuralTyresEntry();
            SetInputs(input, result);

            foreach (var n in _networks.Where(x => x.Key == outputKey)) {
                var normalization = _outputNormalizations[_outputKeys.IndexOf(n.Key)];
                SetValue(result, n.Key, normalization.Denormalize(n.Value.Compute(input)));
            }

            return result[outputKey];
        }

        private static bool IsProceduralValue(string key) {
            return key == NeuralTyresOptions.InputProfile;
        }

        private static double GetValue(NeuralTyresEntry tyre, string key) {
            if (key == NeuralTyresOptions.InputProfile) {
                return tyre[NeuralTyresOptions.InputRadius] - tyre[NeuralTyresOptions.InputRimRadius];
            }

            return tyre[key];
        }

        private static void SetValue(NeuralTyresEntry tyre, string key, double value) {
            tyre[key] = value;
        }

        public class Normalization {
            public double Minimum = double.MaxValue;
            public double Maximum = double.MinValue;
            public double Range = double.NaN;

            public void Extend(double value) {
                if (value < Minimum) Minimum = value;
                if (value > Maximum) Maximum = value;
            }

            public void Seal(double valuePadding) {
                var range = Maximum - Minimum;
                Minimum -= range * valuePadding;
                Maximum += range * valuePadding;
                Range = Maximum - Minimum;
            }

            public double Normalize(double value) {
                return Range == 0d ? 0.5 : (value - Minimum) / Range;
            }

            public bool Fits(double value) {
                return value >= Minimum && value <= Maximum;
            }

            public double Denormalize(double value) {
                return Minimum + value * Range;
            }

            public override string ToString() {
                return $"[{Minimum}…{Maximum}]";
            }

            public static Normalization BuildNormalization(NeuralTyresEntry[] tyres, string key, double valuePadding, out double[] normalizedValues) {
                var result = new Normalization();
                normalizedValues = new double[tyres.Length];
                for (var i = normalizedValues.Length - 1; i >= 0; i--) {
                    var value = GetValue(tyres[i], key);
                    normalizedValues[i] = value;
                    result.Extend(value);
                }
                result.Seal(valuePadding);
                for (var i = normalizedValues.Length - 1; i >= 0; i--) {
                    normalizedValues[i] = result.Normalize(normalizedValues[i]);
                }
                return result;
            }
        }

        public Generated Generate(params double[] input) {
            return null;
        }

        public class Generated {
            public Generated(int version, Dictionary<string, IniFileSection> sections, Dictionary<string, Lut> luts) {
                Version = version;
                Sections = sections;
                Luts = luts;
            }

            public int Version { get; }
            public Dictionary<string, IniFileSection> Sections { get; }
            public Dictionary<string, Lut> Luts { get; }
        }

        public static async Task<TyresMachine> CreateAsync(IEnumerable<NeuralTyresEntry> tyres, NeuralTyresOptions options,
                [CanBeNull] IProgress<Tuple<string, double?>> progress = null, CancellationToken cancellationToken = default(CancellationToken)) {
            var result = new TyresMachine(tyres.ToArray(), options);
            await Task.Run(() => result.Train(progress, cancellationToken)).ConfigureAwait(false);
            return result;
        }

        public static TyresMachine LoadFrom(string filename) {
            return new TyresMachine(filename);
        }
    }
}