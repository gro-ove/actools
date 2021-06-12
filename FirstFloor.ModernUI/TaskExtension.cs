using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI {
    internal static class TaskExtension {
        public static bool IsCanceled([CanBeNull] this Exception e) {
            for (; e != null; e = (e as AggregateException)?.GetBaseException()) {
                if (e is OperationCanceledException) return true;
            }
            return false;
        }

        public static void Ignore(this Task task) {
            task.ContinueWith(x => {
                Logging.Warning(x.Exception?.Flatten());
            }, TaskContinuationOptions.NotOnRanToCompletion);
        }

        public static void Ignore<T>(this Task<T> task) {
            task.ContinueWith(x => {
                Logging.Warning(x.Exception?.Flatten());
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        public static ConfiguredTaskYieldAwaitable ConfigureAwait(this YieldAwaitable yieldAwaitable, bool continueOnCapturedContext) {
            return new ConfiguredTaskYieldAwaitable(continueOnCapturedContext);
        }

        public struct ConfiguredTaskYieldAwaitable {
            private readonly bool _continueOnCapturedContext;

            public ConfiguredTaskYieldAwaitable(bool continueOnCapturedContext) {
                _continueOnCapturedContext = continueOnCapturedContext;
            }

            public ConfiguredTaskYieldAwaiter GetAwaiter() => new ConfiguredTaskYieldAwaiter(_continueOnCapturedContext);
        }

        public struct ConfiguredTaskYieldAwaiter : INotifyCompletion {
            private readonly bool _continueOnCapturedContext;

            public ConfiguredTaskYieldAwaiter(bool continueOnCapturedContext) {
                _continueOnCapturedContext = continueOnCapturedContext;
            }

            public bool IsCompleted => false;

            public void OnCompleted(Action continuation) {
                SynchronizationContext syncContext;
                if (_continueOnCapturedContext && (syncContext = SynchronizationContext.Current) != null) {
                    syncContext.Post(state => ((Action)state)(), continuation);
                } else {
                    ThreadPool.QueueUserWorkItem(state => ((Action)state)(), continuation);
                }
            }

            public void GetResult() { }
        }
    }
}