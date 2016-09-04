using System;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Data;
using FirstFloor.ModernUI.Dialogs;

namespace AcManager.Tools.AcErrors.Solutions {
    using AsyncAction = Func<IAcError, Task>;
    using ProgressAsyncAction = Func<IAcError, CancellationToken, Task>;

    public class AsyncSolution : SolutionBase {
        private readonly AsyncAction _action;
        private readonly ProgressAsyncAction _progressAction;

        public AsyncSolution(string name, string description, AsyncAction action) : base(name, description) {
            _action = action;
        }

        public AsyncSolution(string name, string description, ProgressAsyncAction action) : base(name, description) {
            _progressAction = action;
        }

        public override Task Run(IAcError error, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation) {
            return _progressAction != null ? _progressAction(error, cancellation) : _action(error);
        }
    }
}