using System;

namespace AcTools.NeuralTyres {
    // Do all the syncying yourself
    internal class Progress : IProgress<double> {
        private readonly Action<double> _callback;

        public Progress(Action<double> callback) {
            _callback = callback;
        }

        public void Report(double value) {
            _callback(value);
        }
    }
}