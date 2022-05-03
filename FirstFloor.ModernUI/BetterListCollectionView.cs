using System;
using System.Collections;
using System.Collections.Specialized;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Data;
using FirstFloor.ModernUI.Dialogs;
using FirstFloor.ModernUI.Helpers;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI {
    public class BetterListCollectionView : ListCollectionView, IWeakEventListener {
        private bool _compatible;

        public BetterListCollectionView([NotNull] IList list)
                : base(list) {
            if (list is INotifyCollectionChanged changed) {
                changed.CollectionChanged -= OnCollectionChanged;
                CollectionChangedEventManager.AddListener(changed, this);
            }

            if (Dispatcher != Application.Current.Dispatcher) {
#if DEBUG
                MessageDialog.Show(@"Incorrectly created BetterListCollectionView. Use debugger to trace where it comes from.");
#endif
            }
        }

        public bool ReceiveWeakEvent(Type managerType, object sender, EventArgs e) {
            if (!(e is NotifyCollectionChangedEventArgs notify)) return false;

            if (_compatible) {
                Refresh();
                return true;
            }

            try {
                if (Dispatcher.Thread == Thread.CurrentThread) {
                    OnCollectionChanged(sender, notify);
                } else {
                    Dispatcher.Invoke(() => OnCollectionChanged(sender, notify));
                }
            } catch (ArgumentOutOfRangeException ex) {
                _compatible = true;

                Logging.Debug("That weird ListCollectionView crash, switching to the compatible mode…");
                Logging.Debug(notify.Action);
                Logging.Debug($"Old: {notify.OldStartingIndex}; new: {notify.NewStartingIndex}");
                Logging.Debug($"Old: {string.Join(", ", notify.OldItems.OfType<object>())}; new: {string.Join(", ", notify.NewItems.OfType<object>())}");
                Logging.Debug(ex);

                Refresh();
            } catch (NotSupportedException ex) {
                Logging.Warning(ex.Message);
                Refresh();
            } catch (Exception ex) {
                Logging.Warning(ex);
                Refresh();
            }

            return true;
        }

        public void Refresh([NotNull] object obj) {
            EditItem(obj);
            CommitEdit();
        }
    }
}