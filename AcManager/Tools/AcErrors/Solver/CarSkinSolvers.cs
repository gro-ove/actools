using System.Collections.Generic;
using AcManager.Pages.Dialogs;
using AcManager.Pages.Selected;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;

namespace AcManager.Tools.AcErrors.Solver {
    public class CarSkin_PreviewIsMissingUiSolver : AbstractSolver<CarSkinObject> {
        public CarSkin_PreviewIsMissingUiSolver(CarSkinObject target, AcError error) : base(target, error) {}

        protected override IEnumerable<Solution> GetSolutions() {
            return new [] {
                new Solution(
                        @"Generate new preview",
                        @"Select a new preview using recently used preset",
                        () => {
                            new CarUpdatePreviewsDialog(CarsManager.Instance.GetById(Target.CarId), new[] { Target.Id },
                                    SelectedCarPage.SelectedCarPageViewModel.GetAutoUpdatePreviewsDialogMode()).ShowDialog();
                        }),
                new Solution(
                        @"Setup and generate new preview",
                        @"Select a new preview through settings",
                        () => {
                            new CarUpdatePreviewsDialog(CarsManager.Instance.GetById(Target.CarId), new[] { Target.Id },
                                    CarUpdatePreviewsDialog.DialogMode.Options).ShowDialog();
                        })
            };
        }
    }
}