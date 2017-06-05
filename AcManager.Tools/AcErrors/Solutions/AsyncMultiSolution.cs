using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.AcErrors.Solutions {
    using AsyncAction = Func<IAcError, Task>;
    using AsyncMultiAction = Func<IEnumerable<IAcError>, Task>;
    using ProgressAsyncAction = Func<IAcError, CancellationToken, Task>;
    using ProgressAsyncMultiAction = Func<IEnumerable<IAcError>, CancellationToken, Task>;

    public class AsyncMultiSolution : SolutionBase, IMultiSolution {
        private readonly ProgressAsyncAction _action;
        private readonly ProgressAsyncMultiAction _multiAction;

        public AsyncMultiSolution(string name, string description, AsyncAction action) : this(name, description, (error, token) => action.Invoke(error)) {}

        public AsyncMultiSolution(string name, string description, AsyncMultiAction action) : this(name, description, (errors, token) => action.Invoke(errors)) {}

        public AsyncMultiSolution(string name, string description, ProgressAsyncAction action) : base(name, description) {
            _action = action;
        }

        public AsyncMultiSolution(string name, string description, ProgressAsyncMultiAction action) : base(name, description) {
            _multiAction = action;
        }

        public async Task Run(IEnumerable<IAcError> errors, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            if (_multiAction != null) {
                await _multiAction.Invoke(errors, cancellation);
            } else if (_action != null) {
                var list = errors.ToList();
                for (var i = 0; i < list.Count; i++) {
                    var error = list[i];
                    progress?.Report(error.Target.DisplayName, i, list.Count);
                    Logging.Debug("starting…");
                    await _action.Invoke(error, cancellation).ConfigureAwait(false);
                    Logging.Debug("done!");
                    if (cancellation.IsCancellationRequested) return;
                    Logging.Debug("w0");
                    await Task.Delay(10, cancellation).ConfigureAwait(false);
                    Logging.Debug("wd");
                    if (cancellation.IsCancellationRequested) return;
                    Logging.Debug("ns");
                }
            }
        }

        public override Task Run(IAcError error, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            return Run(new[] { error }, progress, cancellation);
        }
    }
}