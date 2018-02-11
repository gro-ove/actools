/*using AcTools.NeuralTyres.Data;

namespace AcTools.NeuralTyres.Implementations {
    internal class SimpleNetwork : INeuralNetwork {
        private readonly Network _net;

        public SimpleNetwork() {
            _net = new Network(3, new[] { 5, 5 }, 1, 0.8, 0.9, false);
        }

        public void SetOptions(NeuralTyresOptions options) {
            throw new System.NotImplementedException();
        }

        public void Train(double[][] inputs, double[] outputs) {
            var trainSets = outputs.Select((x, i) => new NeuralNetwork.NetworkModels.DataSet(inputs[i], new[] { x })).ToList();
            _net.Train(trainSets, 5000);
        }

        public double Compute(params double[] input) {
            return _net.Compute(input)[0];
        }

        public byte[] Save() {
            throw new System.NotImplementedException();
        }

        public void Load(byte[] data) {
            throw new System.NotImplementedException();
        }

        public void Dispose() {
            throw new System.NotImplementedException();
        }
    }
}*/