using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Presentation;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Helpers {
    public sealed class NonfatalErrorEntry : Displayable {
        public NonfatalErrorEntry(string problemDescription, string solutionCommentary, Exception exception) {
            DisplayName = problemDescription;
            Commentary = solutionCommentary;
            Exception = exception;
        }

        private bool _unseen = true;

        public bool Unseen {
            get { return _unseen; }
            internal set {
                if (Equals(value, _unseen)) return;
                _unseen = value;
                OnPropertyChanged();
                NonfatalError.Instance.UpdateUnseen();
            }
        }

        public string Commentary { get; }

        public Exception Exception { get; }
    }

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

        private static readonly TimeSpan ErrorsTimeout = TimeSpan.FromSeconds(5);
        private const int ErrorsLimit = 30;

        private static void NotifyInner(NonfatalErrorEntry entry) {
            Logging.Warning(entry.DisplayName + ":\n" + entry.Exception);
            Application.Current.Dispatcher.InvokeAsync(() => {
                try {
                    _active = true;

                    if (!_active && DateTime.Now - _previous > ErrorsTimeout) {
                        ErrorMessage.Show(entry);
                    }

                    if (Instance.Errors.Count > ErrorsLimit) {
                        Instance.Errors.RemoveAt(Instance.Errors.Count - 1);
                    }

                    Instance.Errors.Insert(0, entry);
                    if (entry.Unseen) {
                        Instance.UpdateUnseen();
                    }
                } catch(Exception e) {
                    Logging.Error("[NonfatalError] NotifyInner(): " + e);
                } finally {
                    _active = false;
                    _previous = DateTime.Now;
                }
            });
        }

        private static void NotifyInner(string problemDescription, string solutionCommentary, Exception exception) {
            NotifyInner(new NonfatalErrorEntry(problemDescription, solutionCommentary, exception));
        }

        /// <summary>
        /// Notify about some non-fatal exception. User will see some message only if
        /// some notifier (implemented INonfatalErrorNotifier interface) was registered.
        /// </summary>
        /// <param name="problemDescription">Ex.: “Can’t do this and that”.</param>
        /// <param name="solutionCommentary">Ex.: “Make sure A is something and B is something else.”</param>
        /// <param name="exception">Exception which caused the problem.</param>
        public static void Notify([LocalizationRequired] string problemDescription, [LocalizationRequired] string solutionCommentary, Exception exception = null) {
            if (exception is UserCancelledException) return;

            var i = exception as InformativeException;
            if (i != null) {
                NotifyInner(i.Message, i.SolutionCommentary, i.InnerException);
            } else {
                NotifyInner(problemDescription, solutionCommentary, exception);
            }
        }

        /// <summary>
        /// Notify about some non-fatal exception. User will see some message only if
        /// some notifier (implemented INonfatalErrorNotifier interface) was registered.
        /// </summary>
        /// <param name="problemDescription">Ex.: “Can’t do this and that”.</param>
        /// <param name="exception">Exception which caused the problem.</param>
        public static void Notify([LocalizationRequired] string problemDescription, Exception exception = null) {
            Notify(problemDescription, null, exception);
        }
    }
}
