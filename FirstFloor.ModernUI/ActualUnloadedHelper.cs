using System;
using System.Threading.Tasks;
using System.Windows;

namespace FirstFloor.ModernUI {
    public static class ActualUnloadedHelper {
        public static async void OnActualUnload(this FrameworkElement fe, Action action) {
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