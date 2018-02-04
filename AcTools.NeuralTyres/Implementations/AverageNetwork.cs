using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AcTools.NeuralTyres.Data;
using AcTools.Utils.Helpers;

namespace AcTools.NeuralTyres.Implementations {
    internal class AverageNetwork<T> : INeuralNetwork where T : class, INeuralNetwork, new() {
        private NeuralTyresOptions _options = NeuralTyresOptions.Default;
        private List<T> _list;

        public void SetOptions(NeuralTyresOptions options) {
            _options = options;
            _list?.ForEach(x => x.SetOptions(options));
        }

        private void EnsureInitialized() {
            if (_list != null) return;
            _list = Enumerable.Range(0, _options.AverageAmount).Select(x => new T()).ToList();
            _list.ForEach(x => x.SetOptions(_options));
        }

        public void Train(double[][] inputs, double[][] outputs) {
            try {
                EnsureInitialized();
                if (_options.TrainAverageInParallel) {
                    Parallel.ForEach(_list, x => x.Train(inputs, outputs));
                } else {
                    _list.ForEach(x => x.Train(inputs, outputs));
                }
            } catch (Exception e) {
                Console.Error.WriteLine(e);
                throw;
            }
        }

        public double Compute(params double[] input) {
            return _list?.Average(x => x.Compute(input)) ?? 0d;
        }

        public byte[] Save() {
            if (_list == null) return null;

            using (var stream = new MemoryStream()) {
                using (var writer = new BinaryWriter(stream, Encoding.ASCII, true)) {
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