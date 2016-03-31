using System;

namespace AcManager.Tools.AcErrors {
    public class SolvingException : Exception {
        public bool IsCancelled { get; }

        public SolvingException() : base("Cancelled") {
            IsCancelled = true;
        }

        public SolvingException(string message, bool isCancelled = false) : base(message) {
            IsCancelled = isCancelled;
        }
    }
}
