using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using AcManager.Tools.Objects;

namespace AcManager.Tools.AcErrors.Solver {
    public class CarSetup_TrackIsMissingSolver : AbstractSolver<CarSetupObject> {
        public CarSetup_TrackIsMissingSolver(CarSetupObject target, AcError error) : base(target, error) {}

        protected override IEnumerable<Solution> GetSolutions() {
            return new[] {
                new Solution(
                        @"Find track",
                        @"Try to find track online",
                        () => {
                            Process.Start($@"http://assetto-db.com/track/{Target.TrackId}");
                        }),
                new Solution(
                        @"Make generic",
                        @"Move to generic folder",
                        () => {
                            Target.TrackId = null;
                        })
            };
        }
    }
}
