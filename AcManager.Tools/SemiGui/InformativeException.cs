using System;

namespace AcManager.Tools.SemiGui {
    public class InformativeException : Exception {
        public string SolutionCommentary { get; }

        public InformativeException(string message, string solutionCommentary) : base(message) {
            SolutionCommentary = solutionCommentary;
        }
    }
}