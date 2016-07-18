using System;

namespace AcManager.Tools.AcErrors {
    public class SolvingException : Exception {
        public bool IsCancelled { get; }

        /// <summary>
        /// Would work as a cancellation. User will return to solutions selection window.
        /// </summary>
        public SolvingException() : base(Resources.Solving_Cancelled) {
            IsCancelled = true;
        }

        public SolvingException(string message, bool isCancelled = false) : base(message) {
            IsCancelled = isCancelled;
        }
    }
}
