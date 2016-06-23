using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using AcManager.Controls.Pages.Dialogs;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcErrors.Solutions;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcManager.Tools.SemiGui;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace AcManager.Pages.Dialogs {
    public partial class AcErrorSolutionSelector : INotifyPropertyChanged {
        public static IEnumerable<IAcError> GetNearestErrors(AcError error) {
            // all children are here
            // shitty solution, but whatever

            var skin = error.Target as CarSkinObject;
            if (skin != null) {
                return CarsManager.Instance.GetById(skin.CarId)?.Skins.SelectMany(x => x.Errors) ?? new IAcError[0];
            }

            var setup = error.Target as CarSetupObject;
            if (setup != null) {
                return CarsManager.Instance.GetById(setup.CarId)?.GetSetupsManagerIfInitialized()?.LoadedOnly.SelectMany(x => x.Errors) ?? new IAcError[0];
            }

            return new IAcError[0];
        }

        public AcErrorSolutionSelector(AcError acError) {
            InitializeComponent();
            DataContext = this;

            AcError = acError;
            Buttons = new[] { CancelButton };

            if (acError.BaseException != null) {
                ErrorMessage = "Stack trace:\n[mono]" + acError.BaseException + "[/mono]";
            } else {
                Solutions = acError.GetSolutions().ToList();
                if (Solutions.Count == 0) {
                    ErrorMessage = "None of solutions are available.";
                } else {
                    SelectedSolution = Solutions.First();
                    SimilarErrors = Solutions.OfType<IMultiSolution>().Any()
                            ? GetNearestErrors(acError).Where(x => x.Type == acError.Type).ApartFrom(acError).ToList() : new List<IAcError>();
                    MultiAppliable = SimilarErrors.Any();
                }
            }
        }

        private bool _loaded;

        private void OnLoaded(object sender, RoutedEventArgs e) {
            if (_loaded) return;
            _loaded = true;
            AcError.Target.AcObjectOutdated += Target_AcObjectOutdated;
            AcError.Target.Errors.CollectionChanged += Errors_CollectionChanged;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) {
            if (!_loaded) return;
            _loaded = false;
            AcError.Target.AcObjectOutdated -= Target_AcObjectOutdated;
            AcError.Target.Errors.CollectionChanged -= Errors_CollectionChanged;
        }

        private bool _solvingInProgress;
        private bool _needsToBeClosed;

        private void Target_AcObjectOutdated(object sender, EventArgs e) {
            if (_solvingInProgress) {
                _needsToBeClosed = true;
            } else {
                Close();
            }
        }

        private void Errors_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) {
            if (AcError.Target.Errors.Contains(AcError)) return;
            if (_solvingInProgress) {
                _needsToBeClosed = true;
            } else {
                Close();
            }
        }

        public AcError AcError { get; }

        public string ErrorMessage { get; }

        public IReadOnlyList<ISolution> Solutions { get; private set; }

        public IReadOnlyList<IAcError> SimilarErrors { get; }

        public bool MultiAppliable { get; }

        private ISolution _selectedSolution;

        [CanBeNull]
        public ISolution SelectedSolution {
            get { return _selectedSolution; }
            set {
                if (Equals(value, _selectedSolution)) return;
                _selectedSolution = value;
                OnPropertyChanged();
                IsMultiSolutionSelected = value is IMultiSolution;
                RunCommand.OnCanExecuteChanged();
            }
        }

        private bool _isMultiSolutionSelected;

        public bool IsMultiSolutionSelected {
            get { return _isMultiSolutionSelected; }
            set {
                if (Equals(value, _isMultiSolutionSelected)) return;
                _isMultiSolutionSelected = value;
                OnPropertyChanged();
                RunAllCommand.OnCanExecuteChanged();
            }
        }

        private AsyncCommand _runCommand;

        public AsyncCommand RunCommand => _runCommand ?? (_runCommand = new AsyncCommand(async o => {
            var solution = SelectedSolution;
            if (solution == null) return;

            try {
                _solvingInProgress = true;
                if (solution.IsUiSolution) {
                    await solution.Run(AcError, null, CancellationToken.None);
                } else {
                    using (var waiting = new WaitingDialog {
                        Owner = this
                    }) {
                        await solution.Run(AcError, waiting, waiting.CancellationToken);
                        if (waiting.IsCancelled) return;
                    }
                }
            } catch (SolvingException exception) {
                if (!exception.IsCancelled) {
                    NonfatalError.Notify("Can’t solve the problem", exception);
                }
                return;
            } catch (Exception exception) {
                NonfatalError.Notify("Can’t solve the problem", exception);
                return;
            } finally {
                _solvingInProgress = false;
                if (_needsToBeClosed) {
                    Close();
                }
            }
            
            Close();
        }, o => SelectedSolution != null));

        private AsyncCommand _runAllCommand;

        public AsyncCommand RunAllCommand => _runAllCommand ?? (_runAllCommand = new AsyncCommand(async o => {
            var solution = SelectedSolution as IMultiSolution;
            if (solution == null) return;

            try {
                _solvingInProgress = true;
                if (solution.IsUiSolution) {
                    await solution.Run(SimilarErrors.Prepend(AcError), null, CancellationToken.None);
                } else {
                    using (var waiting = new WaitingDialog {
                        Owner = this
                    }) {
                        await solution.Run(SimilarErrors.Prepend(AcError), waiting, waiting.CancellationToken);
                        if (waiting.IsCancelled) return;
                    }
                }
            } catch (SolvingException exception) {
                if (!exception.IsCancelled) {
                    NonfatalError.Notify("Can’t solve the problem", exception);
                }
                return;
            } catch (Exception exception) {
                NonfatalError.Notify("Can’t solve the problem", exception);
                return;
            } finally {
                _solvingInProgress = false;
                if (_needsToBeClosed) {
                    Close();
                }
            }

            Close();
        }, o => IsMultiSolutionSelected));

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
