using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    /// <summary>
    /// Shows non-fatal errors to user or displays them somehow else, depends on implementation.
    /// </summary>
    public class NonfatalError : NotifyPropertyChanged {
        private static DateTime _previous;
        private static bool _active;

        public static void Initialize() {
            _previous = DateTime.Now;
        }

        public static NonfatalError Instance { get; } = new NonfatalError();

        private bool _hasUnseen;

        public bool HasUnseen {
            get { return _hasUnseen; }
            set {
                if (Equals(value, _hasUnseen)) return;
                _hasUnseen = value;
                OnPropertyChanged();
            }
        }

        internal void UpdateUnseen() {
            HasUnseen = Errors.Any(x => x.Unseen);
        }

        public ObservableCollection<NonfatalErrorEntry> Errors { get; } = new ObservableCollection<NonfatalErrorEntry>();

        public ICommand ViewErrorsCommand { get; } = new RelayCommand(o => {
            new NonfatalErrorsDialog().ShowDialog();
        });

        private static readonly TimeSpan ErrorsTimeout = TimeSpan.FromSeconds(3);
        private const int ErrorsLimit = 30;

        private static void NotifyInner(NonfatalErrorEntry entry, bool message) {
            Logging.Warning($"{entry.DisplayName}:\n{entry.Exception}");
            Application.Current.Dispatcher.InvokeAsync(() => {
                try {
                    var active = _active;
                    _active = true;

                    if (message && !active && DateTime.Now - _previous > ErrorsTimeout) {
                        ErrorMessage.Show(entry);
                    }

                    if (Instance.Errors.Count > ErrorsLimit) {
                        Instance.Errors.RemoveAt(Instance.Errors.Count - 1);
                    }

                    Instance.Errors.Insert(0, entry);
                    if (entry.Unseen) {
                        Instance.UpdateUnseen();
                    }
                } catch (Exception e) {
                    Logging.Error("NotifyInner(): " + e);
                } finally {
                    _active = false;
                    _previous = DateTime.Now;
                }
            });
        }

        private static void NotifyInner(string problemDescription, string solutionCommentary, Exception exception,
                [CanBeNull] IEnumerable<INonfatalErrorSolution> solutions, bool message) {
            NotifyInner(new NonfatalErrorEntry(problemDescription, solutionCommentary, exception, solutions ?? new INonfatalErrorSolution[0]), message);
        }

        /// <summary>
        /// Notify about some non-fatal exception. User will see some message.
        /// </summary>
        /// <param name="problemDescription">Ex.: “Can’t do this and that”.</param>
        /// <param name="solutionCommentary">Ex.: “Make sure A is something and B is something else.”</param>
        /// <param name="exception">Exception which caused the problem.</param>
        /// <param name="solutions">A bunch of possible solutions.</param>
        public static void Notify([LocalizationRequired] string problemDescription, [LocalizationRequired] string solutionCommentary, Exception exception = null,
                IEnumerable<INonfatalErrorSolution> solutions = null) {
            if (exception is UserCancelledException) return;

            var i = exception as InformativeException;
            if (i != null) {
                NotifyInner(i.Message, i.SolutionCommentary, i.InnerException, solutions, true);
            } else {
                NotifyInner(problemDescription, solutionCommentary, exception, solutions, true);
            }
        }

        /// <summary>
        /// Notify about some non-fatal exception. User will see some message.
        /// </summary>
        /// <param name="problemDescription">Ex.: “Can’t do this and that”.</param>
        /// <param name="exception">Exception which caused the problem.</param>
        /// <param name="solutions">A bunch of possible solutions.</param>
        public static void Notify([LocalizationRequired] string problemDescription, Exception exception = null,
                IEnumerable<INonfatalErrorSolution> solutions = null) {
            Notify(problemDescription, null, exception, solutions);
        }

        /// <summary>
        /// Notify about some non-fatal exception. User will see a new entry in
        /// errors list.
        /// </summary>
        /// <param name="problemDescription">Ex.: “Can’t do this and that”.</param>
        /// <param name="solutionCommentary">Ex.: “Make sure A is something and B is something else.”</param>
        /// <param name="exception">Exception which caused the problem.</param>
        /// <param name="solutions">A bunch of possible solutions.</param>
        public static void NotifyBackground([LocalizationRequired] string problemDescription, [LocalizationRequired] string solutionCommentary,
                Exception exception = null, IEnumerable<INonfatalErrorSolution> solutions = null) {
            if (exception is UserCancelledException) return;

            var i = exception as InformativeException;
            if (i != null) {
                NotifyInner(i.Message, i.SolutionCommentary, i.InnerException, solutions, false);
            } else {
                NotifyInner(problemDescription, solutionCommentary, exception, solutions, false);
            }
        }

        /// <summary>
        /// Notify about some non-fatal exception. User will see a new entry in
        /// errors list.
        /// </summary>
        /// <param name="problemDescription">Ex.: “Can’t do this and that”.</param>
        /// <param name="exception">Exception which caused the problem.</param>
        /// <param name="solutions">A bunch of possible solutions.</param>
        public static void NotifyBackground([LocalizationRequired] string problemDescription, Exception exception = null,
                IEnumerable<INonfatalErrorSolution> solutions = null) {
            NotifyBackground(problemDescription, null, exception, solutions);
        }
    }
}
