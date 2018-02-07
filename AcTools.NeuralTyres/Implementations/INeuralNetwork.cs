using System;
using System.Threading;
using AcTools.NeuralTyres.Data;
using JetBrains.Annotations;

namespace AcTools.NeuralTyres.Implementations {
    internal interface INeuralNetwork : IDisposable {
        void SetOptions([NotNull] NeuralTyresOptions options);

        void Train([NotNull] double[][] inputs, [NotNull] double[][] outputs, [CanBeNull] IProgress<double> progress, CancellationToken cancellation);

        double[] Compute([NotNull] params double[] input);

        [CanBeNull]
        byte[] Save();

        void Load([NotNull] byte[] data);
    }
}