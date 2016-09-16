using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using AcManager.Tools.AcErrors;
using AcManager.Tools.AcErrors.Solutions;
using AcManager.Tools.Managers;
using AcManager.Tools.Objects;
using AcTools.Utils.Helpers;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace AcManager.Pages.Dialogs {
    public partial class AcErrorSolutionSelector : INotifyPropertyChanged {
        public static IEnumerable<IAcError> GetNearestErrors(AcError error) {
            // all children are here
            // shitty solution, but whatever

            var skin = error.Target as CarSkinObject;
            if (skin != null) {
                var skins = CarsManager.Instance.GetById(skin.CarId)?.SkinsManager;
                return skins?.IsScanned == true ? skins.SelectMany(x => x.Errors) : new IAcError[0];
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
                ErrorMessage = string.Format(AppStrings.AcError_StackTrace, acError.BaseException);
            } else {
                Solutions = acError.GetSolutions().ToList();
                if (Solutions.Count == 0) {
                    ErrorMessage = AppStrings.AcError_SolutionsNotFound;
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

        public AsyncCommand RunCommand => _runCommand ?? (_runCommand = new AsyncCommand(async () => {
            var solution = SelectedSolution;
            if (solution == null) return;

            Logging.Debug($"AcError={AcError.Type}, Solution={solution.Name}");

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
                    NonfatalError.Notify(AppStrings.AcError_CannotSolveProblem, exception);
                }
                return;
            } catch (Exception exception) {
                NonfatalError.Notify(AppStrings.AcError_CannotSolveProblem, exception);
                return;
            } finally {
                _solvingInProgress = false;
                if (_needsToBeClosed) {
                    Close();
                }
            }
            
            Close();
        }, () => SelectedSolution != null));

        private AsyncCommand _runAllCommand;

        public AsyncCommand RunAllCommand => _runAllCommand ?? (_runAllCommand = new AsyncCommand(async () => {
            var solution = SelectedSolution as IMultiSolution;
            if (solution == null) return;

            Logging.Debug($"AcError={AcError.Type}, Solution={solution.Name}");

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
                    NonfatalError.Notify(AppStrings.AcError_CannotSolveProblem, exception);
                }
                return;
            } catch (Exception exception) {
                NonfatalError.Notify(AppStrings.AcError_CannotSolveProblem, exception);
                return;
            } finally {
                _solvingInProgress = false;
                if (_needsToBeClosed) {
                    Close();
                }
            }

            Close();
        }, () => IsMultiSolutionSelected));

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        private void OnPropertyChanged([CallerMemberName] string propertyName = null) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
