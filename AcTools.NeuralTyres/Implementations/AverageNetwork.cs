using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AcTools.NeuralTyres.Data;
using AcTools.Utils;
using AcTools.Utils.Helpers;

namespace AcTools.NeuralTyres.Implementations {
    internal class AverageNetwork<T> : INeuralNetwork where T : class, INeuralNetwork, new() {
        private NeuralTyresOptions _options = NeuralTyresOptions.Default;
        private List<T> _list;

        public void SetOptions(NeuralTyresOptions options) {
            _options = options;
            _list?.ForEach(x => x.SetOptions(options));
        }

        private int _outputSize;

        private void EnsureInitialized(int outputSize) {
            if (_list != null) return;
            _outputSize = outputSize;
            _list = Enumerable.Range(0, _options.AverageAmount).Select(x => new T()).ToList();
            _list.ForEach(x => x.SetOptions(_options));
        }

        public void Train(double[][] inputs, double[][] outputs, IProgress<double> progress, CancellationToken cancellation) {
            EnsureInitialized(_options.SeparateNetworks ? 1 : outputs[0].Length);

            var totalProgress = 0d;
            var summary = _list.Count;

            if (_options.TrainAverageInParallel) {
                Parallel.ForEach(_list, new ParallelOptions {
                    MaxDegreeOfParallelism = _options.MaxDegreeOfParallelism
                }, Run);
            } else {
                _list.ForEach(Run);
            }

            void Run(T x) {
                if (cancellation.IsCancellationRequested) return;
                var previousProgress = 0d;
                x.Train(inputs, outputs, progress == null ? null : new Progress(v => {
                    var currentProgress = (v - previousProgress).AddTo(ref totalProgress);
                    previousProgress = v;
                    progress.Report(currentProgress / summary);
                }), cancellation);
            }
        }

        public double[] Compute(params double[] input) {
            var result = new double[_outputSize];

            var list = _list;
            if (list == null) return result;

            var size = list.Count;
            for (var i = size - 1; i >= 0; i--) {
                var computed = list[i].Compute(input);
                if (computed.Length != result.Length) {
                    throw new Exception("Amount of computed data doesn’t match expected");
                }

                for (var j = result.Length - 1; j >= 0; j--) {
                    result[j] += computed[j] / size;
                }
            }

            return result;
        }

        public byte[] Save() {
            if (_list == null) return null;

            using (var stream = new MemoryStream()) {
                using (var writer = new BinaryWriter(stream, Encoding.ASCII, true)) {
                    writer.Write(_outputSize);
                    foreach (var piece in _list) {
                        var bytes = piece.Save();
                        if (bytes != null) {
                            writer.Write(bytes.Length);
                            writer.Write(bytes);
                        }
                    }
                }

                return stream.ToArray();
            }
        }

        public void Load(byte[] data) {
            DisposeHelper.Dispose(ref _list);

            var list = new List<T>();
            using (var stream = new MemoryStream(data))
            using (var reader = new ReadAheadBinaryReader(stream)) {
                _outputSize = reader.ReadInt32();
                while (reader.Position < reader.Length) {
                    var created = new T();
                    var piece = reader.ReadBytes(reader.ReadInt32());
                    created.Load(piece);
                    list.Add(created);
                }
            }

            _list = list;
            _list.ForEach(x => x.SetOptions(_options));
        }

        public void Dispose() {
            DisposeHelper.Dispose(ref _list);
        }
    }
}