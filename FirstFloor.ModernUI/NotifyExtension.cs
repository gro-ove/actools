using System;
using System.ComponentModel;
using System.Windows;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI {
    public static class NotifyExtension {
        public static void SubscribeWeak([CanBeNull] this INotifyPropertyChanged obj, [CanBeNull] EventHandler<PropertyChangedEventArgs> onPropertyChanged) {
            if (obj == null || onPropertyChanged == null) return;
            WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.AddHandler(obj, nameof(INotifyPropertyChanged.PropertyChanged),
                    onPropertyChanged);
        }

        public static void UnsubscribeWeak([CanBeNull] this INotifyPropertyChanged obj, [CanBeNull] EventHandler<PropertyChangedEventArgs> onPropertyChanged) {
            if (obj == null || onPropertyChanged == null) return;
            WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.RemoveHandler(obj, nameof(INotifyPropertyChanged.PropertyChanged),
                    onPropertyChanged);
        }
    }
}