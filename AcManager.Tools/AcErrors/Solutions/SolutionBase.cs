using System;
using System.Threading;
using System.Threading.Tasks;
using AcManager.Tools.Data;

namespace AcManager.Tools.AcErrors.Solutions {
    public abstract class SolutionBase : ISolution {
        public string Name { get; }

        public string Description { get; }

        /// <summary>
        /// Basically, it shows that solution has its own dialog window from which user can cancel
        /// its execution. Forget to set it — and it will be overlapped by Waiting Dialog.
        /// </summary>
        public bool IsUiSolution { get; set; }

        protected SolutionBase(string name, string description) {
            Name = name;
            Description = description;
        }

        public abstract Task Run(IAcError error, IProgress<AsyncProgressEntry> progress, CancellationToken cancellation);

        public override string ToString() {
            return Name;
        }
    }
}