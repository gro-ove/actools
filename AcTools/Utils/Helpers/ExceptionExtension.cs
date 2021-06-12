using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
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

        public static bool IsWebException([CanBeNull] this Exception e) {
            for (; e != null; e = (e as AggregateException)?.GetBaseException()) {
                if (e is WebException || e is HttpRequestException) return true;
            }

            return false;
        }

        public static IEnumerable<string> FlattenMessage([CanBeNull] this Exception e) {
            var any = false;
            while (e != null) {
                var trimmed = e.Message.TrimEnd('.');
                if (!string.IsNullOrWhiteSpace(trimmed)) {
                    any = true;
                    yield return e.Message.TrimEnd('.');
                }
                e = e.InnerException;
            }
            if (any == false) {
                yield return "Unknown error";
            }
        }
    }
}