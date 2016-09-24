using System;
using System.Collections;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Data;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI {
    public class BetterListCollectionView : ListCollectionView, IWeakEventListener {
        public BetterListCollectionView([NotNull] IList list)
                : base(list) {
            var changed = list as INotifyCollectionChanged;
            if (changed == null) return;

            changed.CollectionChanged -= OnCollectionChanged;
            CollectionChangedEventManager.AddListener(changed, this);
        }

        public bool ReceiveWeakEvent(Type managerType, object sender, EventArgs e) {
            var notify = e as NotifyCollectionChangedEventArgs;
            if (notify == null) return false;

            try {
                OnCollectionChanged(sender, notify);
            } catch (InvalidOperationException ex) {
                Logging.Error(ex);
            }

            return true;
        }
    }
}
