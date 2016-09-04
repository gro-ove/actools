using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FirstFloor.ModernUI.Dialogs;

namespace AcManager.Tools.AcErrors.Solutions {
    public class MultiSolution : Solution, IMultiSolution {
        private readonly Action<IEnumerable<IAcError>> _action;

        public MultiSolution(string name, string description, Action<IAcError> action) : base(name, description, action) { }

        public MultiSolution(string name, string description, Action<IEnumerable<IAcError>> action) : base(name, description, null) {
            _action = action;
        }

        public async Task Run(IEnumerable<IAcError> errors, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            if (_action != null) {
                _action.Invoke(errors);
                return;
            }

            var list = errors.ToList();
            for (var i = 0; i < list.Count; i++) {
                var error = list[i];
                progress?.Report(new AsyncProgressEntry(error.Target.DisplayName, (double)i / list.Count));
                await base.Run(error, progress, cancellation);
                if (cancellation.IsCancellationRequested) return;
                await Task.Delay(10, cancellation);
                if (cancellation.IsCancellationRequested) return;
            }
        }
    }
}