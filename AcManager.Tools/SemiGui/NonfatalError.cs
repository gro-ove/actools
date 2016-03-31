using System;

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
        /// <param name="problemDescription">Ex.: “Can't do this and that”</param>
        /// <param name="solutionCommentary">Ex.: “Make sure A is something and B is something else.”</param>
        /// <param name="exception">Exception which caused the problem</param>
        public static void Notify(string problemDescription, string solutionCommentary, Exception exception = null) {
            _notifier?.Notify(problemDescription, solutionCommentary, exception);
        }

        /// <summary>
        /// Notify about some non-fatal exception. User will see some message only if
        /// some notifier (implemented INonfatalErrorNotifier interface) was registered.
        /// </summary>
        /// <param name="problemDescription">Ex.: “Can't do this and that”</param>
        /// <param name="exception">Exception which caused the problem</param>
        public static void Notify(string problemDescription, Exception exception = null) {
            _notifier?.Notify(problemDescription, null, exception);
        }
    }
}
