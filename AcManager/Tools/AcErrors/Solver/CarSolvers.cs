using System.Collections.Generic;
using AcManager.Pages.Dialogs;
using AcManager.Tools.Objects;

namespace AcManager.Tools.AcErrors.Solver {
    public class Car_ParentIsMissingUiSolver : AbstractSolver<CarObject> {
        public Car_ParentIsMissingUiSolver(CarObject target, AcError error) : base(target, error) {}

        protected override IEnumerable<Solution> GetSolutions() {
            return new [] {
                new Solution(
                    @"Change parent",
                    @"Select a new parent from cars list",
                    () => {
                        new ChangeCarParentDialog(Target).ShowDialog();
                        if (Target.Parent == null) {
                            throw new SolvingException();
                        }
                    })
            };
        }
    }
}
