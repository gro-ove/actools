using System;
using AcManager.Tools.AcObjectsNew;

namespace AcManager.Tools.AcErrors {
    public static class SolversManager {
        private static ISolversFactory _factory; 

        public static void RegisterFactory(ISolversFactory factory) {
            _factory = factory;
        }

        public static ISolver GetSolver(AcObjectNew acObject, AcError error) {
            if (acObject == null) throw new ArgumentNullException(nameof(acObject));
            if (error == null)  throw new ArgumentNullException(nameof(error));

            return _factory?.GetSolver(acObject, error);
        }
    }
}
