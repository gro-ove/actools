using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcErrors.Solver;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;

namespace AcManager.Pages.Dialogs {
    public partial class AcErrorSolutionSelector {
        public static IEnumerable<IAcError> GetNearestErrors(AcCommonObject obj, AcError error) {
            // all children are here
            // shitty solution, but whatever

            var skin = obj as CarSkinObject;
            if (skin != null) {
                return CarsManager.Instance.GetById(skin.CarId)?.Skins.SelectMany(x => x.Errors) ?? new IAcError[0];
            }

            var setup = obj as CarSetupObject;
            if (setup != null) {
                return CarsManager.Instance.GetById(setup.CarId)?.GetSetupsManagerIfInitialized()?.LoadedOnly.SelectMany(x => x.Errors) ?? new IAcError[0];
            }

            return new IAcError[0];
        }

        public AcErrorSolutionSelector(AcCommonObject obj, AcError error) {
            InitializeComponent();
            DataContext = this;

            Title = error.Message;

            if (error.BaseException != null) {
                SetMessageMode("Stack trace:\n[mono]" + error.BaseException + "[/mono]");
            } else {
                var solver = SolversManager.GetSolver(obj, error);
                if (solver == null || !solver.Solutions.Any()) {
                    SetMessageMode();
                } else {
                    SimilarErrors = solver.Solutions.Any(x => x.MultiAppliable)
                            ? GetNearestErrors(obj, error).Where(x => x.Type == error.Type).ApartFrom(error).ToList() : new List<IAcError>();

                    MultiAppliable = SimilarErrors.Any();
                    SetSelectMode(solver);
                }
            }
        }

        public ISolver Solver { get; private set; }

        public List<IAcError> SimilarErrors; 

        public bool MultiAppliable { get; private set; }

        private void SetSelectMode(ISolver solver) {
            SelectModeControl.Visibility = Visibility.Visible;
            Solver = solver;
            SelectModeListBox.SelectedItem = solver.Solutions.FirstOrDefault();

            Buttons = new[] {
                CreateExtraDialogButton(FirstFloor.ModernUI.Resources.Ok, RunCommand, true),
                MultiAppliable ? CreateExtraDialogButton("Solve All Similar Errors", RunAllCommand) : null,
                CancelButton
            }.NonNull();
        }

        private AsyncCommand _runCommand;

        public AsyncCommand RunCommand => _runCommand ?? (_runCommand = new AsyncCommand(async o => {
            var solution = (Solution)SelectModeListBox.SelectedItem;
            try {
                await solution.Run();
            } catch (SolvingException exception) {
                Solver.OnError(solution);
                if (!exception.IsCancelled) {
                    NonfatalError.Notify("Can’t solve the problem", exception);
                }
                return;
            } catch (Exception exception) {
                Solver.OnError(solution);
                NonfatalError.Notify("Can’t solve the problem", exception);
                return;
            }

            Solver.OnSuccess(solution);
            Close();
        }, o => SelectModeListBox.SelectedItem is Solution));

        private AsyncCommand _runAllCommand;

        public AsyncCommand RunAllCommand => _runAllCommand ?? (_runAllCommand = new AsyncCommand(async o => {
            var solution = (Solution)SelectModeListBox.SelectedItem;
            try {
                await solution.Run();
                foreach (var s in SimilarErrors.Select(similarError => SolversManager
                        .GetSolver((AcObjectNew)similarError.Target, (AcError)similarError)
                        .Solutions.FirstOrDefault(x => x.Name == solution.Name)).Where(s => s != null)) {
                    await s.Run();
                }
            } catch (SolvingException exception) {
                Solver.OnError(solution);
                if (!exception.IsCancelled) {
                    NonfatalError.Notify("Can’t solve the problem", exception);
                }
                return;
            } catch (Exception exception) {
                Solver.OnError(solution);
                NonfatalError.Notify("Can’t solve the problem", exception);
                return;
            }

            Solver.OnSuccess(solution);
            Close();
        }, o => (SelectModeListBox.SelectedItem as Solution)?.MultiAppliable == true));

        private void SetMessageMode(string bbCode = null) {
            MessageModeControl.Visibility = Visibility.Visible;
            if (bbCode != null) {
                MessageModeBbCodeBlock.BbCode = bbCode;
            }

            Buttons = new[] { OkButton };
        }

        private void SelectModeListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
            RunAllCommand.OnCanExecuteChanged();
        }
    }
}
