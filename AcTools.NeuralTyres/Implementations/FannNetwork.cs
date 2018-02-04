using System;
using System.IO;
using System.Linq;
using AcTools.NeuralTyres.Data;
using AcTools.Utils.Helpers;
using AcTools.Windows;
using FANNCSharp;
using FANNCSharp.Double;

namespace AcTools.NeuralTyres.Implementations {
    internal class FannNetwork : INeuralNetwork {
        private static readonly string TemporaryFilename = Path.GetTempFileName();

        static FannNetwork() {
            // TODO!
            Kernel32.LoadLibrary(@"D:\Documents\LINQPad Queries\New folder\fanndouble.dll");
        }

        private NeuralTyresOptions _options = NeuralTyresOptions.Default;
        private NeuralNet _net;

        public void SetOptions(NeuralTyresOptions options) {
            _options = options;
        }

        private void EnsureInitialized() {
            if (_net != null) return;

            var layers = _options.NetworkLayers.Select(x => (uint)x).Prepend(3U).Append(1U).ToArray();
            _net = new NeuralNet(NetworkType.LAYER, (uint)layers.Length, layers) {
                LearningRate = (float)_options.LearningRate,
                LearningMomentum = (float)_options.LearningMomentum,
                TrainingAlgorithm = TrainingAlgorithm.TRAIN_INCREMENTAL,
                ActivationFunctionHidden = _options.AccurateCalculations ? ActivationFunction.SIGMOID : ActivationFunction.SIGMOID_STEPWISE,
                ActivationFunctionOutput = _options.AccurateCalculations ? ActivationFunction.SIGMOID : ActivationFunction.SIGMOID_STEPWISE,
            };
        }

        public void Train(double[][] inputs, double[] outputs) {
            try {
                EnsureInitialized();
                _net.RandomizeWeights(0d, 1d);
                var data = new TrainingData();
                var outputsArray = new double[outputs.Length][];
                for (var i = 0; i < outputs.Length; i++) {
                    outputsArray[i] = new[] { outputs[i] };
                }
                data.SetTrainData(inputs, outputsArray);
                for (var i = 500000 / outputs.Length; i > 0; i--) {
                    _net.TrainEpoch(data);
                }
            } catch (Exception e) {
                Console.Error.WriteLine(e);
                throw;
            }
        }

        public double Compute(params double[] input) {
            return _net?.Run(input)[0] ?? 0d;
        }

        public byte[] Save() {
            if (_net == null) return null;
            _net.Save(TemporaryFilename);
            return File.ReadAllBytes(TemporaryFilename);
        }

        public void Load(byte[] data) {
            _net?.Dispose();
            File.WriteAllBytes(TemporaryFilename, data);
            _net = new NeuralNet(TemporaryFilename);
        }

        public void Dispose() {
            _net?.Dispose();
        }
    }
}