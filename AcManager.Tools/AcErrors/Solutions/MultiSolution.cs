using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FirstFloor.ModernUI.Dialogs;

namespace AcManager.Tools.AcErrors.Solutions {
    public class MultiSolution : Solution, IMultiSolution {
        private readonly Action<IAcError> _action;
        private readonly Action<IEnumerable<IAcError>> _multiAction;

        public MultiSolution(string name, string description, Action<IAcError> action) : base(name, description, null) {
            _action = action;
        }

        public MultiSolution(string name, string description, Action<IEnumerable<IAcError>> multiAction) : base(name, description, null) {
            _multiAction = multiAction;
        }

        public async Task Run(IEnumerable<IAcError> errors, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            if (_multiAction != null) {
                _multiAction.Invoke(errors);
            } else if (_action != null) {
                var list = errors.ToList();
                for (var i = 0; i < list.Count; i++) {
                    var error = list[i];
                    progress?.Report(new AsyncProgressEntry(error.Target.DisplayName, (double)i / list.Count));
                    _action.Invoke(error);
                    if (cancellation.IsCancellationRequested) return;
                    await Task.Delay(10, cancellation);
                    if (cancellation.IsCancellationRequested) return;
                }
            }
        }

        public override Task Run(IAcError error, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            return Run(new[] { error }, progress, cancellation);
        }
    }
}