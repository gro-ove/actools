using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using FirstFloor.ModernUI.Commands;
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
            //_previous = DateTime.Now;
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

        public ICommand ViewErrorsCommand { get; } = new DelegateCommand(() => {
            new NonfatalErrorsDialog().ShowDialog();
        });

        private static readonly TimeSpan ErrorsTimeout = TimeSpan.FromSeconds(3);
        private const int ErrorsLimit = 30;

        private static void NotifyInner([NotNull] string message, [CanBeNull] string commentary, [CanBeNull] Exception exception,
                [CanBeNull] IEnumerable<NonfatalErrorSolution> solutions, bool show, string m, string p, int l) {
            if (exception is UserCancelledException) return;

            var i = exception as InformativeException;
            if (i != null) {
                message = i.Message;
                commentary = i.SolutionCommentary;
                exception = i.InnerException;
            }

            Logging.Write('•', $"{message}:\n{exception}", m, p, l);

            var entry = new NonfatalErrorEntry(message, commentary, exception, solutions ?? new NonfatalErrorSolution[0]);
            ActionExtension.InvokeInMainThreadAsync(() => {
                try {
                    var active = _active;
                    _active = true;

                    if (show && !active && DateTime.Now - _previous > ErrorsTimeout) {
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
                    Logging.Error(e);
                } finally {
                    _active = false;
                    _previous = DateTime.Now;
                }
            });
        }

        /// <summary>
        /// Notify about some non-fatal exception. User will see some message.
        /// </summary>
        /// <param name="message">Ex.: “Can’t do this and that”.</param>
        /// <param name="commentary">Ex.: “Make sure A is something and B is something else.”</param>
        /// <param name="exception">Exception which caused the problem.</param>
        /// <param name="solutions">A bunch of possible solutions.</param>
        /// <param name="m">Member at which error occured.</param>
        /// <param name="p">File at which error occured.</param>
        /// <param name="l">Line at which error occured.</param>
        public static void Notify([LocalizationRequired, NotNull] string message, [LocalizationRequired, CanBeNull] string commentary, Exception exception = null,
                IEnumerable<NonfatalErrorSolution> solutions = null, [CallerMemberName] string m = null, [CallerFilePath] string p = null,
                [CallerLineNumber] int l = -1) {
            NotifyInner(message, commentary, exception, solutions, true, m, p, l);
        }

        /// <summary>
        /// Notify about some non-fatal exception. User will see some message.
        /// </summary>
        /// <param name="message">Ex.: “Can’t do this and that”.</param>
        /// <param name="exception">Exception which caused the problem.</param>
        /// <param name="solutions">A bunch of possible solutions.</param>
        /// <param name="m">Member at which error occured.</param>
        /// <param name="p">File at which error occured.</param>
        /// <param name="l">Line at which error occured.</param>
        public static void Notify([LocalizationRequired, NotNull] string message, Exception exception = null,
                IEnumerable<NonfatalErrorSolution> solutions = null, [CallerMemberName] string m = null, [CallerFilePath] string p = null,
                [CallerLineNumber] int l = -1) {
            NotifyInner(message, null, exception, solutions, true, m, p, l);
        }

        /// <summary>
        /// Notify about some non-fatal exception. User will see a new entry in
        /// errors list.
        /// </summary>
        /// <param name="message">Ex.: “Can’t do this and that”.</param>
        /// <param name="commentary">Ex.: “Make sure A is something and B is something else.”</param>
        /// <param name="exception">Exception which caused the problem.</param>
        /// <param name="solutions">A bunch of possible solutions.</param>
        /// <param name="m">Member at which error occured.</param>
        /// <param name="p">File at which error occured.</param>
        /// <param name="l">Line at which error occured.</param>
        public static void NotifyBackground([LocalizationRequired, NotNull] string message, [LocalizationRequired, CanBeNull] string commentary,
                Exception exception = null, IEnumerable<NonfatalErrorSolution> solutions = null, [CallerMemberName] string m = null,
                [CallerFilePath] string p = null, [CallerLineNumber] int l = -1) {
            NotifyInner(message, commentary, exception, solutions, false, m, p, l);
        }

        /// <summary>
        /// Notify about some non-fatal exception. User will see a new entry in
        /// errors list.
        /// </summary>
        /// <param name="message">Ex.: “Can’t do this and that”.</param>
        /// <param name="exception">Exception which caused the problem.</param>
        /// <param name="solutions">A bunch of possible solutions.</param>
        /// <param name="m">Member at which error occured.</param>
        /// <param name="p">File at which error occured.</param>
        /// <param name="l">Line at which error occured.</param>
        public static void NotifyBackground([LocalizationRequired, NotNull] string message, Exception exception = null,
                IEnumerable<NonfatalErrorSolution> solutions = null, [CallerMemberName] string m = null, [CallerFilePath] string p = null,
                [CallerLineNumber] int l = -1) {
            NotifyInner(message, null, exception, solutions, false, m, p, l);
        }
    }
}
