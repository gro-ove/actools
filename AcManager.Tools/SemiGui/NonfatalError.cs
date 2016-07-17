using System;
using JetBrains.Annotations;

namespace AcManager.Tools.SemiGui {
    /// <summary>
    /// Shows non-fatal errors to user or displays them somehow else, depends on implementation.
    /// </summary>
    public static class NonfatalError {
        private static INonfatalErrorNotifier _notifier;

        public static void Register(INonfatalErrorNotifier notifier) {
            _notifier = notifier;
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
                _notifier?.Notify(i.Message, i.SolutionCommentary, null);
            } else {
                _notifier?.Notify(problemDescription, solutionCommentary, exception);
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
