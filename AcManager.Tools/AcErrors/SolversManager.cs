using System;
using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.AcErrors.Solver;
using AcManager.Tools.AcObjectsNew;
using AcTools.Utils.Helpers;

namespace AcManager.Tools.AcErrors {
    public static class SolversManager {
        private static readonly List<ISolversFactory> Factories; 

        public static void RegisterFactory(ISolversFactory factory) {
            Factories.Add(factory);
        }

        public static ISolver GetSolver(AcObjectNew acObject, AcError error) {
            if (acObject == null) throw new ArgumentNullException(nameof(acObject));
            if (error == null)  throw new ArgumentNullException(nameof(error));

            var solvers = Factories.Select(x => x.GetSolver(acObject, error)).Where(x => x != null).ToIReadOnlyListIfItsNot();
            return solvers.Count > 1 ? new CombinedSolver(solvers.Reverse()) : solvers.FirstOrDefault();
        }

        static SolversManager() {
            Factories = new List<ISolversFactory>();
            RegisterFactory(new DefaultFactory());   
        }
    }
}
