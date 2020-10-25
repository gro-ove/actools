using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace AcTools.Utils.Helpers {
    public static class TaskExtension {
        /// <summary>
        /// Use .Ignore() instead, it captures exceptions properly.
        /// </summary>
        public static void Forget(this Task task) { }

        /// <summary>
        /// Use .Ignore() instead, it captures exceptions properly.
        /// </summary>
        public static void Forget<T>(this Task<T> task) { }

        public static void Ignore(this Task task) {
            task.ContinueWith(x => { AcToolsLogging.Write(x.Exception?.Flatten()); }, TaskContinuationOptions.NotOnRanToCompletion);
        }

        public static void Ignore<T>(this Task<T> task) {
            task.ContinueWith(x => { AcToolsLogging.Write(x.Exception?.Flatten()); }, TaskContinuationOptions.OnlyOnFaulted);
        }

        public static IEnumerable<Task> MakeList(params Func<Task>[] functions) {
            return functions.Select(x => x());
        }

        public static async Task WithCancellation(this Task task, CancellationToken cancellationToken) {
            var tcs = new TaskCompletionSource<bool>();
            using (cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs)) {
                if (task != await Task.WhenAny(task, tcs.Task).ConfigureAwait(false)) {
                    throw new OperationCanceledException(cancellationToken);
                }
                await task; // already completed; propagate any exception
            }
        }

        public static async Task<T> WithCancellation<T>(this Task<T> task, CancellationToken cancellationToken) {
            var tcs = new TaskCompletionSource<bool>();
            using (cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).TrySetResult(true), tcs)) {
                if (task != await Task.WhenAny(task, tcs.Task).ConfigureAwait(false)) {
                    throw new OperationCanceledException(cancellationToken);
                }
                return await task; // already completed; propagate any exception
            }
        }

        public static async Task WhenAll(this IEnumerable<Task> tasks, int limit, CancellationToken cancellation = default) {
            var list = new List<Task>(limit);
            foreach (var task in tasks) {
                if (cancellation.IsCancellationRequested) return;

                list.Add(task);
                if (list.Count == limit) {
                    var completedTask = await Task.WhenAny(list);
                    list.Remove(completedTask);
                    await completedTask;
                }
            }

            if (list.Any()) {
                await Task.WhenAll(list);
            }
        }

        public static async Task WhenAll(this IEnumerable<Task> tasks) {
            await Task.WhenAll(tasks);
        }

        public static async Task<IEnumerable<T>> WhenAll<T>(this IEnumerable<Task<T>> tasks, int limit,
                CancellationToken cancellation = default) {
            var list = new List<Task<T>>(limit);
            var result = new List<T>();

            foreach (var task in tasks) {
                if (cancellation.IsCancellationRequested) return result;

                list.Add(task);
                if (list.Count != limit) continue;

                var temp = await Task.WhenAny(list);
                result.Add(temp.Result);
                list.Remove(temp);
            }

            if (list.Any()) {
                result.AddRange(await Task.WhenAll(list));
            }
            return result;
        }

        public static async Task<IEnumerable<T>> WhenAll<T>(this IEnumerable<Task<T>> tasks) {
            return await Task.WhenAll(tasks);
        }

        public static Task<TBase> FromDerived<TBase, TDerived>(this Task<TDerived> task) where TDerived : TBase {
            var tcs = new TaskCompletionSource<TBase>();
            task.ContinueWith(t => tcs.SetResult(t.Result), TaskContinuationOptions.OnlyOnRanToCompletion);
            task.ContinueWith(t => {
                if (t.Exception == null) return;
                tcs.SetException(t.Exception.InnerExceptions);
            }, TaskContinuationOptions.OnlyOnFaulted);
            task.ContinueWith(t => tcs.SetCanceled(), TaskContinuationOptions.OnlyOnCanceled);
            return tcs.Task;
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