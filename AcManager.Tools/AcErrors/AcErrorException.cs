using System;

namespace AcManager.Tools.AcErrors {
    public class AcErrorException : Exception {
        public readonly AcError AcError;

        public AcErrorException(AcErrorType type, params object[] args) {
            AcError = new AcError(type, args);
        }
    }
}
