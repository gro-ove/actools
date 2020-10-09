using System;
using System.Net;
using System.Text;
using JetBrains.Annotations;

namespace AcTools.Utils.Helpers {
    public static class ExceptionExtension {
        public static bool IsCancelled([CanBeNull] this Exception e) {
            for (; e != null; e = (e as AggregateException)?.GetBaseException()) {
                if (e is OperationCanceledException
                        || e is WebException we && we.Status == WebExceptionStatus.RequestCanceled) return true;
            }

            return false;
        }

        public static string FlattenMessage([CanBeNull] this Exception e, string joinWith = "\n") {
            var result = new StringBuilder();
            while (e != null) {
                if (result.Length > 0) {
                    result.Append(joinWith);
                }
                result.Append(e.Message.TrimEnd('.'));
                e = e.InnerException;
            }
            var joined = result.ToString().Trim();
            return string.IsNullOrEmpty(joined) ? "Unknown error" : joined;
        }
    }
}