/*using AcTools.NeuralTyres.Data;

namespace AcTools.NeuralTyres.Implementations {
    internal class ConvNetwork : INeuralNetwork {
        private readonly Net<double> _net;

        public ConvNetworkPiece() {
            _net = new Net<double>();
            _net.AddLayer(new InputLayer(1, 1, 3));
            _net.AddLayer(new FullyConnLayer(15));
            _net.AddLayer(new ReluLayer());
            _net.AddLayer(new FullyConnLayer(15));
            _net.AddLayer(new SigmoidLayer());
            _net.AddLayer(new FullyConnLayer(1));
            _net.AddLayer(new RegressionLayer());
        }

        public void SetOptions(NeuralTyresOptions options) {
            throw new System.NotImplementedException();
        }

        public void Train(double[][] inputs, double[] outputs) {
            var trainer = new SgdTrainer(_net) { LearningRate = 0.003, Momentum = 0.5 };

            var input = BuilderInstance.Volume.SameAs(new Shape(1, 1, 3, inputs.Length));
            var output = BuilderInstance.Volume.SameAs(new Shape(1, 1, 1, inputs.Length));

            for (var i = 0; i < inputs.Length; i++) {
                for (var k = 0; k < 3; k++) {
                    input.Set(0, 0, k, i, inputs[i][k]);
                }

                output.Set(0, 0, 0, i, outputs[i]);
            }

            for (var j = 0; j < 5000; j++) {
                trainer.Train(input, output);
            }
        }

        public double Compute(params double[] input) {
            return _net.Forward(new Volume(input, new Shape(3))).Get(0);
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