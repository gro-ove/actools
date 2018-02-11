using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcTools.NeuralTyres.Data;
using AcTools.NeuralTyres.Implementations;
using AcTools.Utils;
using AcTools.Utils.Helpers;
using AcTools.Utils.Physics;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AcTools.NeuralTyres {
    public interface ITyresMachineExtras {
        void OnSave(ZipArchive archive, JObject manifest);
        void OnLoad(ZipArchive archive, JObject manifest);
    }

    public class TyresMachine {
        [NotNull]
        private readonly NeuralTyresOptions _options;

        [NotNull]
        public NeuralTyresOptions Options => _options;

        [CanBeNull]
        private INeuralNetwork[] _networks;

        [CanBeNull]
        private INeuralNetwork _singleNetwork;

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
            _outputKeys = tyres[0].Keys.Where(x => Array.IndexOf(options.IgnoredKeys, x) == -1
                    && (options.OverrideOutputKeys == null || Array.IndexOf(options.OverrideOutputKeys, x) != -1)).ToArray();
            _outputNormalizations = new Normalization[_outputKeys.Length];
            _outputNormalized = new double[_outputKeys.Length][];

            for (var i = 0; i < _outputKeys.Length; i++) {
                _outputNormalizations[i] = Normalization.BuildNormalization(tyres, _outputKeys[i], options.ValuePadding,
                        out _outputNormalized[i]);
            }
        }

        private readonly int SaveFormatVersion = 1;

        private TyresMachine([NotNull] Stream stream, [CanBeNull] ITyresMachineExtras extras) {
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read, false)) {
                var manifest = Read<JObject>("Manifest.json");
                if (manifest.GetIntValueOnly("version") != SaveFormatVersion) {
                    throw new Exception("Unsupported version: " + manifest.GetIntValueOnly("version"));
                }

                TyresVersion = manifest.GetIntValueOnly("tyresVersion", 0);
                if (TyresVersion < 7) {
                    throw new Exception("Unsupported tyres version: " + TyresVersion);
                }

                extras?.OnLoad(zip, manifest);

                _options = Read<NeuralTyresOptions>("Options.json");
                _tyresSources = Read<NeuralTyresSource[]>("Input/Sources.json");
                _luts = Read<Dictionary<string, string>[]>("Input/LUTs.json")
                        .Select(x => (IReadOnlyDictionary<string, Lut>)x.ToDictionary(y => y.Key, y => Lut.FromValue(y.Value))).ToArray();
                _inputNormalizations = Read<Normalization[]>("Input/Normalizations.json");
                _outputKeys = Read<string[]>("Output/Keys.json");
                _outputNormalizations = Read<Normalization[]>("Output/Normalizations.json");

                if (_options.SeparateNetworks) {
                    _networks = new INeuralNetwork[_outputKeys.Length];
                    for (var i = 0; i < _outputKeys.Length; i++) {
                        var key = _outputKeys[i];
                        var typeName = zip.ReadString($"Networks/{key}/Type.txt");
                        var type = Type.GetType(typeName);
                        if (type == null) {
                            throw new Exception("Type not found: " + typeName);
                        }

                        var instance = (INeuralNetwork)Activator.CreateInstance(type);
                        instance.SetOptions(_options);
                        instance.Load(zip.ReadBytes($"Networks/{key}/Data.bin"));
                        _networks[i] = instance;
                    }
                } else {
                    var typeName = zip.ReadString("Network/Type.txt");
                    var type = Type.GetType(typeName);
                    if (type == null) {
                        throw new Exception("Type not found: " + typeName);
                    }

                    var instance = (INeuralNetwork)Activator.CreateInstance(type);
                    instance.SetOptions(_options);
                    instance.Load(zip.ReadBytes("Network/Data.bin"));
                    _singleNetwork = instance;
                }

                T Read<T>(string key) {
                    return JsonConvert.DeserializeObject<T>(zip.ReadString(key));
                }
            }
        }

        private TyresMachine([NotNull] string filename, [CanBeNull] ITyresMachineExtras extras) : this(File.OpenRead(filename), extras) { }
        private TyresMachine([NotNull] byte[] data, [CanBeNull] ITyresMachineExtras extras) : this(new MemoryStream(data), extras) { }

        public void Save([NotNull] Stream stream, [CanBeNull] ITyresMachineExtras extras) {
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true)) {
                Add("Options.json", _options);
                Add("Input/Sources.json", _tyresSources);
                Add("Input/LUTs.json", _luts.Select(x => x.Where(y => y.Value != null).ToDictionary(y => y.Key, y => y.Value.ToString())));
                Add("Input/Normalizations.json", _inputNormalizations);
                Add("Output/Keys.json", _outputKeys);
                Add("Output/Normalizations.json", _outputNormalizations);

                if (_networks != null) {
                    for (var i = 0; i < _networks.Length; i++) {
                        var key = _outputKeys[i];
                        var network = _networks[i];
                        var networkType = network.GetType().FullName;
                        var data = network.Save();
                        if (networkType == null || data == null) continue;
                        zip.AddString($"Networks/{key}/Type.txt", networkType);
                        zip.AddBytes($"Networks/{key}/Data.bin", data);
                    }
                } else if (_singleNetwork != null) {
                    var networkType = _singleNetwork.GetType().FullName;
                    var data = _singleNetwork.Save();
                    if (networkType != null && data != null) {
                        zip.AddString("Network/Type.txt", networkType);
                        zip.AddBytes("Network/Data.bin", data);
                    }
                }

                var manifest = new JObject {
                    ["version"] = SaveFormatVersion,
                    ["tyresVersion"] = TyresVersion
                };

                extras?.OnSave(zip, manifest);
                Add("Manifest.json", manifest);

                void Add(string key, object data) {
                    zip.AddString(key, JsonConvert.SerializeObject(data, Formatting.Indented));
                }
            }
        }

        public byte[] ToByteArray([CanBeNull] ITyresMachineExtras extras) {
            using (var stream = new MemoryStream()) {
                Save(stream, extras);
                return stream.ToArray();
            }
        }

        public void Save([NotNull] string filename, [CanBeNull] ITyresMachineExtras extras) {
            using (var stream = File.Create(filename)) {
                Save(stream, extras);
            }
        }

        private void Train([CanBeNull] IProgress<Tuple<string, double?>> progress, CancellationToken cancellationToken) {
            var inputs = _inputNormalized;
            var outputs = _outputNormalized;
            if (inputs == null || outputs == null) {
                throw new Exception("This instance can’t be trained");
            }

            var keys = _outputKeys;
            if (keys.Length == 0) {
                throw new ArgumentException("At least one output key is required");
            }

            if (_options.SeparateNetworks) {
                var networks = new INeuralNetwork[keys.Length];
                _networks = networks;

                var lastFinished = keys[0];
                double processed = 0, total = keys.Length;
                Parallel.ForEach(keys.Select((x, i) => new {
                    Index = i,
                    Key = x,
                    Outputs = outputs[i].Select(y => new[] { y }).ToArray(),
                }), new ParallelOptions {
                    MaxDegreeOfParallelism = _options.MaxDegreeOfParallelism
                }, value => {
                    if (cancellationToken.IsCancellationRequested) return;

                    var network = CreateNeuralNetwork();
                    lock (networks) {
                        networks[value.Index] = network;
                    }

                    var previousProgress = 0d;
                    network.SetOptions(_options);
                    network.Train(inputs, value.Outputs, progress == null ? null : new Progress(v => {
                        var delta = v - previousProgress;
                        if (delta < 0.05) return;

                        var currentProgress = delta.AddTo(ref processed);
                        previousProgress = v;
                        progress.Report(Tuple.Create(lastFinished, (double?)currentProgress / total));
                    }), cancellationToken);

                    lastFinished = value.Key;
                });
            } else {
                var network = CreateNeuralNetwork();
                _singleNetwork = network;
                network.SetOptions(_options);

                var flipped = new double[outputs[0].Length][];
                for (var i = 0; i < flipped.Length; i++) {
                    flipped[i] = new double[outputs.Length];
                    for (var j = 0; j < outputs.Length; j++) {
                        flipped[i][j] = outputs[j][i];
                    }
                }

                network.Train(inputs, flipped, progress == null ? null : new Progress(v => { progress.Report(Tuple.Create("Combined", (double?)v)); }),
                        cancellationToken);
            }
        }

        [Pure]
        private INeuralNetwork CreateNeuralNetwork() {
            if (_options.AverageAmount > 1) {
                return new AverageNetwork<FannNetwork>();
            }

            return new FannNetwork();
        }

        public Normalization GetNormalization(string key) {
            return _inputNormalizations.ArrayElementAtOrDefault(_options.InputKeys.IndexOf(key));
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

        private void PrepareInputs(double[] input) {
            for (var i = 0; i < _options.InputKeys.Length; i++) {
                input[i] = _inputNormalizations[i].Normalize(input[i]);
            }
        }

        public NeuralTyresEntry Conjure(params double[] input) {
            var name = Sources.Select(x => x.Name).GroupBy(x => x).MaxEntry(x => x.Count()).Key;
            var shortName = Sources.Select(x => x.ShortName).GroupBy(x => x).MaxEntry(x => x.Count()).Key;
            var result = new NeuralTyresEntry(name, shortName, TyresVersion);
            SetInputs(input, result);

            if (_singleNetwork != null) {
                var data = _singleNetwork.Compute(input);
                if (data.Length != _outputKeys.Length) {
                    throw new Exception($"Amount of computed data doesn’t match output keys: {data.Length}≠{_outputKeys.Length}");
                }

                for (var i = 0; i < _outputKeys.Length; i++) {
                    SetValue(result, _outputKeys[i], _outputNormalizations[i].Denormalize(data[i]));
                }
            } else if (_networks != null) {
                for (var i = 0; i < _networks.Length; i++) {
                    SetValue(result, _outputKeys[i], _outputNormalizations[i].Denormalize(_networks[i].Compute(input)[0]));
                }
            } else {
                throw new Exception("Invalid state");
            }

            return result;
        }

        public double Conjure(string outputKey, params double[] input) {
            PrepareInputs(input);

            var keyIndex = _outputKeys.IndexOf(outputKey);
            if (keyIndex == -1) {
                throw new Exception($"Not supported key: {keyIndex}");
            }

            var normalization = _outputNormalizations[keyIndex];
            if (_singleNetwork != null) {
                var data = _singleNetwork.Compute(input);
                if (data.Length != _outputKeys.Length) {
                    throw new Exception("Amount of computed data doesn’t match output keys");
                }

                return normalization.Denormalize(data[keyIndex]);
            }

            if (_networks != null) {
                return normalization.Denormalize(_networks[keyIndex].Compute(input)[0]);
            }

            throw new Exception("Invalid state");
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

            [Pure]
            public double Normalize(double value) {
                return Range == 0d ? 0.5 : ((value - Minimum) / Range).Saturate();
            }

            [Pure]
            public bool Fits(double value) {
                var safetyPadding = Range * 0.0001f;
                return value >= Minimum - safetyPadding && value <= Maximum + safetyPadding;
            }

            [Pure]
            public double Denormalize(double value) {
                return Minimum + value * Range;
            }

            [Pure]
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

        [ItemCanBeNull]
        public static async Task<TyresMachine> CreateAsync(IEnumerable<NeuralTyresEntry> tyres, NeuralTyresOptions options,
                [CanBeNull] IProgress<Tuple<string, double?>> progress = null, CancellationToken cancellationToken = default) {
            var result = new TyresMachine(tyres.ToArray(), options);
            await Task.Run(() => result.Train(progress, cancellationToken), cancellationToken).ConfigureAwait(false);
            return cancellationToken.IsCancellationRequested ? null : result;
        }

        public static TyresMachine LoadFrom([NotNull] string filename, [CanBeNull] ITyresMachineExtras extras) {
            return new TyresMachine(filename, extras);
        }

        public static TyresMachine LoadFrom([NotNull] byte[] data, [CanBeNull] ITyresMachineExtras extras) {
            return new TyresMachine(data, extras);
        }
    }
}