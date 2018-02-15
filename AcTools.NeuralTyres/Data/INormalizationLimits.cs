using System;
using JetBrains.Annotations;

namespace AcTools.NeuralTyres.Data {
    public interface INormalizationLimits {
        [CanBeNull]
        Tuple<double, double> GetLimits([NotNull] string key);
    }
}