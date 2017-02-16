using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace AcTools.Render.Base.Utils {
    internal interface ICancellable {
        void Cancel();
    }

    internal sealed class ThreadPool : IDisposable {
        public ThreadPool(string name, int size, ThreadPriority priority) {
            _workers = new LinkedList<Thread>();
            for (var i = 0; i < size; ++i) {
                var worker = new Thread(Worker) {
                    Name = $"{name} ({i})",
                    IsBackground = true,
                    Priority = priority
                };

                worker.Start();
                _workers.AddLast(worker);
            }
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
                foreach (var worker in _workers.ToList()) {
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
}