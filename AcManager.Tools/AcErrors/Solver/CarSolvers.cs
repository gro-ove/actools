using System.Collections.Generic;
using System.Linq;
using AcManager.Tools.Objects;

namespace AcManager.Tools.AcErrors.Solver {
    public class Car_ParentIsMissingSolver : AbstractSolver<CarObject> {
        public Car_ParentIsMissingSolver(CarObject target, AcError error) : base(target, error) {}

        protected override IEnumerable<Solution> GetSolutions() {
            return new [] {
                new Solution(
                    @"Make independent",
                    @"Remove id of missing parent from ui_car.json",
                    () => {
                        Target.ParentId = null;
                    })
            }.Concat(TryToFindRenamedFile(Target.Location, Target.JsonFilename)).Where(x => x != null);
        }
    }
}
