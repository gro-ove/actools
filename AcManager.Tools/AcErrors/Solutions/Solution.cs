using System;
using System.Threading;
using System.Threading.Tasks;
using FirstFloor.ModernUI.Dialogs;
using JetBrains.Annotations;

namespace AcManager.Tools.AcErrors.Solutions {
    public class Solution : SolutionBase {
        [CanBeNull]
        private readonly Action<IAcError> _action;

        public Solution(string name, string description, [CanBeNull] Action<IAcError> action) : base(name, description) {
            _action = action;
        }

        public override Task Run(IAcError error, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            _action?.Invoke(error);
            return Task.Delay(0, cancellation);
        }
    }
}
