using System;
using System.Linq;
using System.Windows;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcErrors.Solver;
using AcManager.Tools.AcObjectsNew;
using AcManager.Tools.Helpers;
using AcManager.Tools.SemiGui;

namespace AcManager.Pages.Dialogs {
    public partial class AcErrorSolutionSelector {
        public AcErrorSolutionSelector(AcObjectNew obj, AcError error) {
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
                    SetSelectMode(solver);
                }
            }
        }

        public ISolver Solver { get; private set; }

        private void SetSelectMode(ISolver solver) {
            SelectModeControl.Visibility = Visibility.Visible;
            Solver = solver;
            SelectModeListBox.SelectedItem = solver.Solutions.FirstOrDefault();

            Buttons = new[] { OkButton, CancelButton };

            Closing += AcErrorSolutionSelector_Closing;
        }

        void AcErrorSolutionSelector_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
            if (!IsResultOk) return;

            var solution = SelectModeListBox.SelectedItem as Solution;
            if (solution == null) {
                e.Cancel = true;
                return;
            }

            try {
                solution.Action.Invoke();
            } catch (SolvingException exception) {
                Solver.OnError(solution);
                if (!exception.IsCancelled) {
                    NonfatalError.Notify("Can't solve the problem", exception);
                }
                return;
            } catch (Exception exception) {
                Solver.OnError(solution);
                NonfatalError.Notify("Can't solve the problem", exception);
                return;
            }

            Solver.OnSuccess(solution);
        }

        private void SetMessageMode(string bbCode = null) {
            MessageModeControl.Visibility = Visibility.Visible;
            if (bbCode != null) {
                MessageModeBbCodeBlock.BbCode = bbCode;
            }

            Buttons = new[] { OkButton };
        }
    }
}
