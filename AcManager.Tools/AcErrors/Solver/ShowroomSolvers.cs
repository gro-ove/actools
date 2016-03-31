using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.Objects;
using AcTools.Kn5File;

namespace AcManager.Tools.AcErrors.Solver {
    public class Showroom_Kn5IsMissingSolver : AbstractSolver<ShowroomObject> {
        public Showroom_Kn5IsMissingSolver(ShowroomObject target, AcError error) : base(target, error) {}

        protected override IEnumerable<Solution> GetSolutions() {
            return new [] {
                new Solution(
                    @"Make an empty model",
                    @"With nothing, only emptyness",
                    () => {
                        Kn5.CreateEmpty().SaveAll(Target.Kn5Filename);
                    })
            }.Concat(TryToFindAnyFile(Target.Location, Target.Kn5Filename, "*.kn5")).Where(x => x != null);
        }
    }
}
