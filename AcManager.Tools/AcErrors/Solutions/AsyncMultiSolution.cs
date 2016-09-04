using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Data;
using FirstFloor.ModernUI.Dialogs;

namespace AcManager.Tools.AcErrors.Solutions {
    using AsyncAction = Func<IAcError, Task>;
    using ProgressAsyncAction = Func<IAcError, CancellationToken, Task>;

    public class AsyncMultiSolution : AsyncSolution, IMultiSolution {
        public AsyncMultiSolution(string name, string description, AsyncAction action) : base(name, description, action) { }
        public AsyncMultiSolution(string name, string description, ProgressAsyncAction action) : base(name, description, action) { }

        public async Task Run(IEnumerable<IAcError> errors, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
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