using System;

namespace AcTools.Processes {
    public class ShotingCancelledException : Exception {
        public readonly bool UserCancelled;

        public ShotingCancelledException() {
            UserCancelled = true;
        }

        public ShotingCancelledException(string message) : base(message) {}

        public ShotingCancelledException(string message, Exception innerException) : base(message, innerException) {}
    }
}