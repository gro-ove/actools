using System;
using JetBrains.Annotations;

namespace AcTools.Utils.Helpers {
    public static class ProgressExtension {
        private class SubrangeProgress : IProgress<double> {
            private readonly IProgress<double> _baseProgress;
            private readonly double _from;
            private readonly double _range;

            public SubrangeProgress(IProgress<double> baseProgress, double from, double range) {
                _baseProgress = baseProgress;
                _from = from;
                _range = range;
            }

            public void Report(double value) {
                _baseProgress?.Report(_from + value * _range);
            }
        }

        [NotNull]
        public static IProgress<double> Subrange([CanBeNull] this IProgress<double> baseProgress, double from, double range) {
            return new SubrangeProgress(baseProgress, from, range);
        }

        [NotNull]
        public static IProgress<double> ToDouble([CanBeNull] this IProgress<Tuple<string, double?>> baseProgress, [CanBeNull] string message) {
            return new Progress<double>(v => {
                baseProgress?.Report(new Tuple<string, double?>(message, v));
            });
        }
    }
}