using System;
using System.ComponentModel;
using System.Windows;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI {
    public static class NotifyExtension {
        public static T SubscribeWeak<T>([CanBeNull] this T obj, [CanBeNull] EventHandler<PropertyChangedEventArgs> onPropertyChanged)
                where T : INotifyPropertyChanged {
            if (obj != null && onPropertyChanged != null) {
                WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.AddHandler(obj, nameof(INotifyPropertyChanged.PropertyChanged),
                        onPropertyChanged);
            }

            return obj;
        }

        public static T UnsubscribeWeak<T>([CanBeNull] this T obj, [CanBeNull] EventHandler<PropertyChangedEventArgs> onPropertyChanged)
                where T : INotifyPropertyChanged {
            if (obj != null && onPropertyChanged != null) {
                WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.RemoveHandler(obj, nameof(INotifyPropertyChanged.PropertyChanged),
                        onPropertyChanged);
            }
            return obj;
        }
    }
}