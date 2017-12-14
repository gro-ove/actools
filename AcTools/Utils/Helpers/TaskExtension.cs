using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace AcTools.Utils.Helpers {
    public class TaskCache {
        private Dictionary<long, Task> _running = new Dictionary<long, Task>();

        public Task Get(Func<Task> fn, params object[] arguments) {
            lock (_running) {
                var checksum = arguments.Aggregate<object, long>(0, (current, t) => (current * 397) ^ t.GetHashCode());
                if (_running.TryGetValue(checksum, out var running) && !running.IsCanceled && !running.IsCompleted && !running.IsFaulted) {
                    return running;
                }

                var task = fn();
                if (!task.IsCanceled && !task.IsCompleted && !task.IsFaulted) {
                    _running[checksum] = task;
                    task.ContinueWith(v => {
                        lock (_running) {
                            _running.Remove(checksum);
                        }
                    });
                }

                return task;
            }
        }

        public Task<T> Get<T>(Func<Task<T>> fn, params object[] arguments) {
            lock (_running) {
                var checksum = arguments.Aggregate<object, long>(typeof(T).Name.GetHashCode(), (current, t) => (current * 397) ^ t.GetHashCode());
                if (_running.TryGetValue(checksum, out var running) && !running.IsCanceled && !running.IsCompleted && !running.IsFaulted) {
                    return (Task<T>)running;
                }

                var task = fn();
                if (!task.IsCanceled && !task.IsCompleted && !task.IsFaulted) {
                    _running[checksum] = task;
                    task.ContinueWith(v => {
                        lock (_running) {
                            _running.Remove(checksum);
                        }
                    });
                }

                return task;
            }
        }
    }

    public static class TaskExtension {
        public static bool IsCanceled([CanBeNull] this Exception e) {
            for (; e != null; e = (e as AggregateException)?.GetBaseException()) {
                if (e is OperationCanceledException
                        || e is WebException we && we.Status == WebExceptionStatus.RequestCanceled) return true;
            }

            return false;
        }

        public static void Forget(this Task task) { }
        public static void Forget<T>(this Task<T> task) { }

        public static void Ignore(this Task task) {
            task.ContinueWith(x => {
                AcToolsLogging.Write(x.Exception?.Flatten());
            }, TaskContinuationOptions.NotOnRanToCompletion);
        }

        public static void Ignore<T>(this Task<T> task) {
            task.ContinueWith(x => {
                AcToolsLogging.Write(x.Exception?.Flatten());
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        public static async Task WhenAll(this IEnumerable<Task> tasks, int limit, CancellationToken cancellation = default(CancellationToken)) {
            var list = new List<Task>(limit);
            foreach (var task in tasks) {
                if (cancellation.IsCancellationRequested) return;

                list.Add(task);
                if (list.Count == limit) {
                    list.Remove(await Task.WhenAny(list));
                }
            }

            if (list.Any()) {
                await Task.WhenAll(list);
            }
        }

        public static async Task WhenAll(this IEnumerable<Task> tasks) {
            await Task.WhenAll(tasks);
        }

        public static async Task<IEnumerable<T>> WhenAll<T>(this IEnumerable<Task<T>> tasks, int limit, CancellationToken cancellation = default(CancellationToken)) {
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
    }
}

