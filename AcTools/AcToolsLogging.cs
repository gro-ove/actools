using System;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace AcTools {
    public static class AcToolsLogging {
        public static Action<string, string, string, int> Logger;
        public static Action<string, string, Exception> NonFatalErrorHandler;

        public static void Write(object s, [CallerMemberName] string m = null, [CallerFilePath] string p = null, [CallerLineNumber] int l = -1) {
            if (Logger == null) {
                Console.WriteLine(s?.ToString() ?? "<NULL>");
            } else {
                Logger.Invoke(s?.ToString() ?? "<NULL>", m, p, l);
            }
        }

        /// <summary>
        /// Notify about some non-fatal exception. User will see some message.
        /// </summary>
        /// <param name="message">Ex.: “Can’t do this and that”.</param>
        /// <param name="commentary">Ex.: “Make sure A is something and B is something else.”</param>
        /// <param name="exception">Exception which caused the problem.</param>
        public static void NonFatalErrorNotify([NotNull] string message, [CanBeNull] string commentary, Exception exception = null) {
            if (Logger == null) {
                Console.WriteLine("Non-fatal error: " + message);

                if (commentary != null) {
                    Console.WriteLine(commentary);
                }

                if (exception != null) {
                    Console.WriteLine(exception);
                }
            } else {
                NonFatalErrorHandler.Invoke(message, commentary, exception);
            }
        }
    }
}