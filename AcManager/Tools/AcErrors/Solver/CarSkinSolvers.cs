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
                        @"Generate a new preview using recently used preset",
                        () => {
                            if (!new CarUpdatePreviewsDialog(CarsManager.Instance.GetById(Target.CarId), new[] { Target.Id },
                                    SelectedCarPage.SelectedCarPageViewModel.GetAutoUpdatePreviewsDialogMode()).ShowDialog()) {
                                throw new SolvingException();
                            }
                        }) {
                            MultiAppliable = true
                        },
                new Solution(
                        @"Setup and generate new preview",
                        @"Select a new preview through settings",
                        () => {
                            if (!new CarUpdatePreviewsDialog(CarsManager.Instance.GetById(Target.CarId), new[] { Target.Id },
                                    CarUpdatePreviewsDialog.DialogMode.Options).ShowDialog()) {
                                throw new SolvingException();
                            }
                        })
            };
        }
    }

    public class CarSkin_LiveryIsMissingUiSolver : AbstractSolver<CarSkinObject> {
        public CarSkin_LiveryIsMissingUiSolver(CarSkinObject target, AcError error) : base(target, error) {}

        protected override IEnumerable<Solution> GetSolutions() {
            return new [] {
                new Solution(
                        @"Generate new livery",
                        @"Generate a new livery using last settings of Livery Editor",
                        () => LiveryIconEditor.GenerateAsync(Target)) {
                            MultiAppliable = true
                        },
                new Solution(
                        @"Setup new livery",
                        @"Select a new livery using Livery Editor",
                        () => {
                            if (!new LiveryIconEditor(Target).ShowDialog()) {
                                throw new SolvingException();
                            }
                        })
            };
        }
    }
}