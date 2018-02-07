using System;
using System.IO;
using System.Linq;
using System.Threading;
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

        private int _outputSize;

        private void EnsureInitialized(int outputSize) {
            if (_net != null) return;
            Console.WriteLine($"Output size: {outputSize}");
            _outputSize = outputSize;
            var layers = _options.Layers.Select(x => (uint)x).Prepend(3U).Append((uint)outputSize).ToArray();
            _net = new NeuralNet(NetworkType.LAYER, (uint)layers.Length, layers) {
                LearningRate = (float)_options.LearningRate,
                LearningMomentum = (float)_options.LearningMomentum,
                TrainingAlgorithm = (TrainingAlgorithm)_options.FannAlgorithm,
                ActivationFunctionHidden = _options.HighPrecision ? ActivationFunction.SIGMOID : ActivationFunction.SIGMOID_STEPWISE,
                ActivationFunctionOutput = _options.HighPrecision ? ActivationFunction.SIGMOID : ActivationFunction.SIGMOID_STEPWISE,
            };
        }

        public void Train(double[][] inputs, double[][] outputs, IProgress<double> progress, CancellationToken cancellation) {
            Console.WriteLine($"Inputs: {inputs.Length}, outputs: {outputs.Length}");

            EnsureInitialized(_options.SeparateNetworks ? 1 : outputs[0].Length);
            _net.RandomizeWeights(-_options.RandomBounds, _options.RandomBounds);

            var data = new TrainingData();
            data.SetTrainData(inputs, outputs);

            const int iterations = 100;
            var runs = _options.TrainingRuns / outputs.Length;
            var runsPerStep = runs / iterations;
            var finishedRuns = 0;

            for (var j = 0; j < iterations; j++) {
                if (cancellation.IsCancellationRequested) return;
                progress?.Report((double)j / iterations);
                for (var i = runsPerStep; i > 0; i--) {
                    finishedRuns++;
                    _net.TrainEpoch(data);
                }
            }

            for (; finishedRuns < runs; finishedRuns++) {
                if (cancellation.IsCancellationRequested) return;
                _net.TrainEpoch(data);
            }
        }

        private bool _computeMode;

        private void SetComputeMode() {
            var net = _net;
            if (_computeMode || net == null) return;
            _computeMode = true;

            net.ActivationFunctionHidden = ActivationFunction.SIGMOID;
            net.ActivationFunctionOutput = ActivationFunction.SIGMOID;
        }

        public double[] Compute(params double[] input) {
            SetComputeMode();
            return _net?.Run(input) ?? new double[_outputSize];
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