using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI {
    public static class ActualUnloadedHelper {
        public static void SubscribeWeak([CanBeNull] this INotifyPropertyChanged obj, [CanBeNull] FrameworkElement parent,
                [CanBeNull] EventHandler<PropertyChangedEventArgs> onPropertyChanged) {
            if (parent == null || obj == null || onPropertyChanged == null) return;
            obj.SubscribeWeak(onPropertyChanged);
            parent.OnActualUnload(() => {
                obj.UnsubscribeWeak(onPropertyChanged);
            });
        }

        public static async void OnActualUnload([NotNull] this FrameworkElement fe, Action action) {
            var unloading = false;

            fe.Loaded += (sender, args) => {
                unloading = false;
            };

            fe.Unloaded += async (sender, args) => {
                unloading = true;
                await Task.Delay(2000);
                if (unloading && !fe.IsLoaded) {
                    action?.Invoke();
                    action = null;

                    unloading = false;
                }
            };

            await Task.Delay(3000);
            if (!unloading && !fe.IsLoaded) {
                action?.Invoke();
                action = null;
            }
        }
    }
}