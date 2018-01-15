using System;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;

namespace AcTools {
    public delegate void LogHandler(string message, string memberName = null, string filePath = null, int lineNumber = -1);
    public delegate void NonFatalErrorHandler(string message, string commentary, Exception exception, bool isBackground);

    public static class AcToolsLogging {
        public static LogHandler Logger;
        public static NonFatalErrorHandler NonFatalErrorHandler;

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
                NonFatalErrorHandler.Invoke(message, commentary, exception, false);
            }
        }

        /// <summary>
        /// Notify about some non-fatal exception. User will see some message.
        /// </summary>
        /// <param name="message">Ex.: “Can’t do this and that”.</param>
        /// <param name="commentary">Ex.: “Make sure A is something and B is something else.”</param>
        /// <param name="exception">Exception which caused the problem.</param>
        public static void NonFatalErrorNotifyBackground([NotNull] string message, [CanBeNull] string commentary, Exception exception = null) {
            if (Logger == null) {
                Console.WriteLine("Non-fatal error: " + message);

                if (commentary != null) {
                    Console.WriteLine(commentary);
                }

                if (exception != null) {
                    Console.WriteLine(exception);
                }
            } else {
                NonFatalErrorHandler.Invoke(message, commentary, exception, true);
            }
        }
    }
}