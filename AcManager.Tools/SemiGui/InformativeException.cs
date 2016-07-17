using System;
using JetBrains.Annotations;

namespace AcManager.Tools.SemiGui {
    public class InformativeException : Exception {
        public string SolutionCommentary { get; }

        public InformativeException([LocalizationRequired] string message, [LocalizationRequired] string solutionCommentary) : base(message) {
            SolutionCommentary = solutionCommentary;
        }
    }
}