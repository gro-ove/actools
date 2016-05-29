using System;
using AcManager.Tools.AcObjectsNew;

namespace AcManager.Tools.AcErrors {
    public class AcErrorException : Exception {
        public readonly AcError AcError;

        public AcErrorException(IAcObjectNew target, AcErrorType type, params object[] args) {
            AcError = new AcError(target, type, args);
        }
    }
}
