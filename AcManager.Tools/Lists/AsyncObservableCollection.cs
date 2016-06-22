using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Threading;

namespace AcManager.Tools.Lists {
    [Obsolete]
    public class AsyncObservableCollection<T> : ObservableCollection<T> {

        private readonly SynchronizationContext _synchronizationContext = SynchronizationContext.Current;

        public AsyncObservableCollection() {
            // if (_synchronizationContext == null) throw  new Exception("_synchronizationContext = null");
        }

        public AsyncObservableCollection(IEnumerable<T> list)
            : base(list) {
            // if (_synchronizationContext == null) throw  new Exception("_synchronizationContext = null");
        }

        public void AddSafe(T obj) {
            lock (this) {
                Add(obj);
            }
        }

        protected override void OnCollectionChanged(NotifyCollectionChangedEventArgs e) {
            if (_synchronizationContext == null) {
                //Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)delegate() {
                //    RaiseCollectionChanged(e);
                //});
                // I have no idea what I’m doing
            } else if (SynchronizationContext.Current == _synchronizationContext) {
                RaiseCollectionChanged(e);
            } else {
                _synchronizationContext.Send(RaiseCollectionChanged, e);
            }
        }

        private void RaiseCollectionChanged(object param) {
            base.OnCollectionChanged((NotifyCollectionChangedEventArgs)param);
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e) {
            if (_synchronizationContext == null) {
                //Application.Current.Dispatcher.Invoke(DispatcherPriority.Normal, (Action)delegate() {
                //    RaisePropertyChanged(e);
                //});
            } else if (SynchronizationContext.Current == _synchronizationContext) {
                RaisePropertyChanged(e);
            } else {
                _synchronizationContext.Send(RaisePropertyChanged, e);
            }
        }

        private void RaisePropertyChanged(object param) {
            base.OnPropertyChanged((PropertyChangedEventArgs)param);
        }
    }
}
