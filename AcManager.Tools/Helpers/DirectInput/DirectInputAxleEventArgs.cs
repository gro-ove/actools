using System;
using JetBrains.Annotations;

namespace AcManager.Tools.Helpers.DirectInput {
    public class DirectInputAxleEventArgs {
        public DirectInputAxleEventArgs([NotNull] DirectInputAxle axle, double delta) {
            if (axle == null) throw new ArgumentNullException(nameof(axle));

            Axle = axle;
            Value = axle.Value;
            Delta = delta;
        }

        [NotNull]
        public DirectInputAxle Axle { get; }

        public double Value { get; }

        public double Delta { get; }
    }
}