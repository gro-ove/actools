/* If assembly has SmartThreadPool in references, you can use it instead of 
 * primitive custom thread pool. Might be more reliable, although I’m not
 * sure */
#define SMART_POOL

/* Include primitive custom thread pool from SO as well. I highly recommend
 * to use at either smart pool or custom, default C# one simply doesn’t work
 * in these conditions properly. */
// #define CUSTOM_POOL

using System;
#if CUSTOM_POOL
using FirstFloor.ModernUI.Helpers;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
#endif

namespace FirstFloor.ModernUI.Windows.Controls {
    public partial class BetterImage {
        /// <summary>
        /// Number of threads to decode bitmaps.
        /// </summary>
        public static int OptionNumberOfThreads = 3;

        /// <summary>
        /// Type of thread pool used to decode bitmaps.
        /// </summary>
        public static ThreadPoolType OptionThreadPoolType =
#if SMART_POOL
                ThreadPoolType.Smart;
#elif CUSTOM_POOL
                ThreadPoolType.Custom;
#else
                ThreadPoolType.Default;
#endif

        private static IThreadPool _threadPool;

        internal static IThreadPool ThreadPool {
            get {
                if (_threadPool == null) {
                    switch (OptionThreadPoolType) {
                        case ThreadPoolType.Default:
                            _threadPool = ThreadPools.Default;
                            break;
#if CUSTOM_POOL
                        case ThreadPoolType.Custom:
                            _threadPool = ThreadPools.Custom;
                            break;
#endif
#if SMART_POOL
                        case ThreadPoolType.Smart:
                            _threadPool = ThreadPools.Smart;
                            break;
#endif
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }

                return _threadPool;
            }
        }

        public enum ThreadPoolType {
            Default,
#if CUSTOM_POOL
            Custom,
#endif
#if SMART_POOL
            Smart,
#endif
        }

        public static class ThreadPools {
            public static readonly IThreadPool Default = new DefaultInner();

            private class DefaultInner : IThreadPool {
                public ICancellable Run(Action action) {
                    System.Threading.ThreadPool.QueueUserWorkItem(o => action());
                    return null;
                }
            }

#if CUSTOM_POOL
            public static readonly IThreadPool Custom = new CustomInner();

            private class CustomInner : IThreadPool {
                private sealed class Pool : IDisposable {
                    public Pool(int size) {
                        _workers = new LinkedList<Thread>();
                        for (var i = 0; i < size; ++i) {
                            var worker = new Thread(Worker) {
                                Name = $"Worker {i}",
                                IsBackground = true,
                                Priority = ThreadPriority.BelowNormal
                            };

                            worker.Start();
                            _workers.AddLast(worker);
                        }

                        Application.Current.Exit += OnExit;
                    }

                    private void OnExit(object sender, ExitEventArgs e) {
                        Dispose();
                    }

                    public void Dispose() {
                        var waitForThreads = false;
                        lock (_tasks) {
                            if (!_disposed) {
                                _disallowAdd = true;

                                // wait for all tasks to finish processing while not allowing any more new tasks
                                while (_tasks.Count > 0) {
                                    Monitor.Wait(_tasks);
                                }

                                _disposed = true;
                                Monitor.PulseAll(_tasks);
                                
                                // wake all workers (none of them will be active at this point; disposed flag will cause then to finish so that we can join them)
                                waitForThreads = true;
                            }
                        }

                        if (waitForThreads) {
                            foreach (var worker in _workers) {
                                worker.Join();
                            }
                        }
                    }

                    public ICancellable QueueTask(Action task) {
                        lock (_tasks) {
                            if (_disallowAdd) {
                                throw new InvalidOperationException("This Pool instance is in the process of being disposed, can't add anymore");
                            }

                            if (_disposed) {
                                throw new ObjectDisposedException("This Pool instance has already been disposed");
                            }

                            _tasks.AddLast(task);

                            // pulse because tasks count changed
                            Monitor.PulseAll(_tasks);

                            return new CancelTask(_tasks, task);
                        }
                    }

                    private class CancelTask : ICancellable {
                        private readonly LinkedList<Action> _tasks;
                        private readonly Action _task;

                        public CancelTask(LinkedList<Action> tasks, Action task) {
                            _tasks = tasks;
                            _task = task;
                        }

                        public void Cancel() {
                            lock (_tasks) {
                                Logging.Debug(_tasks.Contains(_task));
                                _tasks.Remove(_task);
                            }
                        }
                    }

                    private void Worker() {
                        while (true) {
                            // loop until threadpool is disposed
                            Action task;

                            lock (_tasks) {
                                // finding a task needs to be atomic

                                while (true) {
                                    // wait for our turn in _workers queue and an available task
                                    if (_disposed) return;

                                    if (null != _workers.First && ReferenceEquals(Thread.CurrentThread, _workers.First.Value) && _tasks.Count > 0) {
                                        // we can only claim a task if its our turn (this worker thread is the first entry in _worker queue) and there is a task available

                                        task = _tasks.First.Value;
                                        _tasks.RemoveFirst();
                                        _workers.RemoveFirst();

                                        // pulse because current (First) worker changed (so that next available sleeping worker will pick up its task)
                                        Monitor.PulseAll(_tasks);

                                        // we found a task to process, break out from the above 'while (true)' loop
                                        break;
                                    }

                                    // go to sleep, either not our turn or no task to process
                                    Monitor.Wait(_tasks);
                                }
                            }

                            // process the found task
                            task();

                            lock (_tasks) {
                                _workers.AddLast(Thread.CurrentThread);
                            }
                        }
                    }

                    // queue of worker threads ready to process actions
                    private readonly LinkedList<Thread> _workers;

                    // actions to be processed by worker threads
                    private readonly LinkedList<Action> _tasks = new LinkedList<Action>();

                    // set to true when disposing queue but there are still tasks pending
                    private bool _disallowAdd;

                    // set to true when disposing queue and no more tasks are pending
                    private bool _disposed;
                }

                private static Pool _pool;

                public ICancellable Run(Action action) {
                    if (_pool == null) {
                        _pool = new Pool(OptionNumberOfThreads);
                    }

                    return _pool.QueueTask(action);
                }
            }
#endif

#if SMART_POOL
            public static readonly IThreadPool Smart = new SmartInner();

            private class SmartInner : IThreadPool {
                private static Amib.Threading.SmartThreadPool _pool;

                private class CancellableWrapper : ICancellable {
                    private readonly Amib.Threading.IWorkItemResult _item;

                    public CancellableWrapper(Amib.Threading.IWorkItemResult item) {
                        _item = item;
                    }

                    public void Cancel() {
                        _item.Cancel();
                    }
                }

                public ICancellable Run(Action action) {
                    if (_pool == null) {
                        _pool = new Amib.Threading.SmartThreadPool(new Amib.Threading.STPStartInfo {
                            MaxWorkerThreads = OptionNumberOfThreads,
                            AreThreadsBackground = true,
                            EnableLocalPerformanceCounters = false,
                            WorkItemPriority = Amib.Threading.WorkItemPriority.BelowNormal
                        });
                    }

                    return new CancellableWrapper(_pool.QueueWorkItem(new Amib.Threading.Action(action)));
                }
            }
#endif
        }
    }
}