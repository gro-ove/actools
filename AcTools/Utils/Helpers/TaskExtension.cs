using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AcTools.Utils.Helpers {
    public static class TaskExtension {
        public static void Forget(this Task task) { }

        public static void Forget<T>(this Task<T> task) { }

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

