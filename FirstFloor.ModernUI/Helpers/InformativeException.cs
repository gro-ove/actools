using System;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    public class InformativeException : Exception {
        [CanBeNull]
        public string SolutionCommentary { get; }

        public InformativeException([LocalizationRequired] string message, Exception innerException = null) : base(message, innerException) {
            SolutionCommentary = null;
        }

        public InformativeException([LocalizationRequired] string message, [LocalizationRequired] string solutionCommentary, Exception innerException = null)
                : base(message, innerException) {
            SolutionCommentary = solutionCommentary;
        }

        public string ToSingleString() {
            return SolutionCommentary == null ? $"{Message}." : $"{Message}. {SolutionCommentary}";
        }

        public override string ToString() {
            return base.ToString() + "\nCommentary: " + SolutionCommentary;
        }
    }
}