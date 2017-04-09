using System;
using System.ComponentModel;
using System.Windows;

namespace FirstFloor.ModernUI {
    public static class NotifyExtension {
        public static void SubscribeWeak(this INotifyPropertyChanged obj, EventHandler<PropertyChangedEventArgs> onPropertyChanged) {
            if (obj == null || onPropertyChanged == null) return;
            WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.AddHandler(obj, nameof(INotifyPropertyChanged.PropertyChanged),
                    onPropertyChanged);
        }

        public static void UnsubscribeWeak(this INotifyPropertyChanged obj, EventHandler<PropertyChangedEventArgs> onPropertyChanged) {
            if (obj == null || onPropertyChanged == null) return;
            WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.RemoveHandler(obj, nameof(INotifyPropertyChanged.PropertyChanged),
                    onPropertyChanged);
        }
    }
}