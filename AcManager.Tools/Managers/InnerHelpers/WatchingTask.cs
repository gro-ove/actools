using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using AcManager.Tools.Helpers;
using FirstFloor.ModernUI;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.Managers.InnerHelpers {
    internal class WatchingTask {
        public WatchingTask(string location, IWatchingChangeApplier applier) {
            _location = location;
            _applier = applier;
        }

        private readonly string _location;
        private readonly IWatchingChangeApplier _applier;
        private readonly Queue<WatchingChange> _queue = new Queue<WatchingChange>();
        private bool _delay, _actionInProcess;

        [MethodImpl(MethodImplOptions.Synchronized)]
        private void Action() {
            Debug.WriteLine("ACMGR: WatchingTask.Action()");
            if (_actionInProcess) return;
            _actionInProcess = true;
            AsyncAction().Forget();
        }

        private async Task AsyncAction() {
            Debug.WriteLine("ACMGR: WatchingTask.AsyncAction()");

            while (_delay) {
                _delay = false;
                Debug.WriteLine("ACMGR: WatchingTask.AsyncAction() Delay");

                int delayAmount;
                lock (_queue) {
                    delayAmount = _queue.Any() && _queue.Peek().Type == WatcherChangeTypes.Deleted ? 300 : 200;
                }

                await Task.Delay(delayAmount);
            }

            ActionExtension.InvokeInMainThread(() => {
                try {
                    Debug.WriteLine("ACMGR: WatchingTask.AsyncAction() Invoke");
                    // in some cases (CREATED, DELETED) queue could be cleared

                    lock (_queue) {
                        if (_queue.Any()) {
                            var change = _queue.Dequeue();
                            _applier.ApplyChange(_location, change);

                            if (_queue.Any()) {
                                if (change.Type == WatcherChangeTypes.Changed) {
                                    // very special case:
                                    // after CHANGED could be only CHANGED, and only with FULL_FILENAME
                                    // let’s process all of them in one INVOKE

                                    foreach (var next in _queue) {
                                        _applier.ApplyChange(_location, next);
                                    }
                                    _queue.Clear();
                                } else {
                                    Debug.WriteLine("ACMGR: WatchingTask.AsyncAction() Next");
                                    AsyncAction().Forget();
                                    return;
                                }
                            }
                        }
                    }

                    _delay = false;
                    _actionInProcess = false;
                } catch (Exception e) {
                    Logging.Error(e);
                }
            });
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void AddEvent(WatcherChangeTypes type, string newLocation, string fullFilename) {
            Debug.WriteLine("ACMGR: WatchingTask.AddEvent({0}, {1})", type, newLocation);

            // try to collapse new entry with the last one (mostly for optimization purposes)
            lock (_queue) {
                if (_queue.Any()) {
                    var last = _queue.Last();

                    switch (type) {
                        case WatcherChangeTypes.Created:
                            if (last.Type == WatcherChangeTypes.Renamed) {
                                // RENAMED-CREATED occasion
                                _queue.Enqueue(new WatchingChange {
                                    Type = WatcherChangeTypes.Created
                                });
                            } else if (last.Type == WatcherChangeTypes.Deleted) {
                                // DELETED-CREATED occasion, replace by CHANGED
                                // why we clear queue so easily? maybe there is something before DELETED?
                                // well, take a look at the next case
                                _queue.Clear();
                                _queue.Enqueue(new WatchingChange { Type = WatcherChangeTypes.Changed });
                            }

                            // CREATED-CREATED, CHANGED-CREATED ignored
                            break;

                        case WatcherChangeTypes.Deleted:
                            if (last.Type == WatcherChangeTypes.Created) {
                                // special CREATED-DELETED occasion, this way AcManager won’t be bothered
                                _queue.Clear();
                            } else if (last.Type == WatcherChangeTypes.Changed) {
                                // CHANGED-DELETED case, remove useless CHANGED
                                _queue.Clear();
                                _queue.Enqueue(new WatchingChange { Type = WatcherChangeTypes.Deleted });
                            }

                            // RENAMED-DELETED and DELETED-DELETED ignored
                            break;

                        case WatcherChangeTypes.Renamed:
                            // sort of the most important case, outweighs everything else
                            _queue.Clear();
                            _queue.Enqueue(new WatchingChange { Type = WatcherChangeTypes.Renamed, NewLocation = newLocation });
                            break;

                        case WatcherChangeTypes.All:
                            // what does it mean? all?
                            break;

                        case WatcherChangeTypes.Changed:
                            // some file inside was changed
                            // if last entry is CHANGED too, we should add FULL_FILENAME for smart reload
                            // FULL_FILENAME=null means that changes can’t be processes with smart reload

                            if (last.Type == WatcherChangeTypes.Changed && last.FullFilename != null) {
                                if (fullFilename == null) {
                                    last.FullFilename = null;
                                } else if (!_queue.Any(x => string.Equals(x.FullFilename, fullFilename,
                                        StringComparison.OrdinalIgnoreCase))) {
                                    _queue.Enqueue(new WatchingChange {
                                        Type = WatcherChangeTypes.Changed,
                                        FullFilename = fullFilename
                                    });
                                }
                            }
                            break;

                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    _delay = true;
                } else {
                    // if queue is empty, add a new entry to queue and run start processing queue
                    _queue.Enqueue(new WatchingChange {
                        Type = type,
                        FullFilename = fullFilename,
                        NewLocation = newLocation
                    });
                    _delay = true;
                    Action();
                }
            }
        }
    }
}
