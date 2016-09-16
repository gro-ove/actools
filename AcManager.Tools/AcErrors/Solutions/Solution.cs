using System;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Data;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.AcErrors.Solutions {
    public class Solution : SolutionBase {
        private readonly Action<IAcError> _action;

        public Solution(string name, string description, Action<IAcError> action) : base(name, description) {
            _action = action;
        }

        public override Task Run(IAcError error, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            _action.Invoke(error);
            return Task.Delay(0, cancellation);
        }
    }
}
