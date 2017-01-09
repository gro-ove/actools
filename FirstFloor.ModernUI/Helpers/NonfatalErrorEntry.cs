using System;
using System.Collections.Generic;
using System.Linq;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    public sealed class NonfatalErrorEntry : Displayable {
        public NonfatalErrorEntry(string problemDescription, string solutionCommentary, Exception exception,
                [NotNull] IEnumerable<NonfatalErrorSolution> solutions) {
            if (solutions == null) throw new ArgumentNullException(nameof(solutions));

            DisplayName = problemDescription;
            Commentary = solutionCommentary;
            Exception = exception;
            Solutions = solutions as IReadOnlyList<NonfatalErrorSolution> ?? solutions.ToList();
        }

        private bool _unseen = true;

        public bool Unseen {
            get { return _unseen; }
            internal set {
                if (Equals(value, _unseen)) return;
                _unseen = value;
                OnPropertyChanged();
                NonfatalError.Instance.UpdateUnseen();
            }
        }

        public string Commentary { get; }

        public Exception Exception { get; }

        [NotNull]
        public IReadOnlyList<NonfatalErrorSolution> Solutions { get; set; }

        public bool HasSolutions => Solutions.Any();
    }
}