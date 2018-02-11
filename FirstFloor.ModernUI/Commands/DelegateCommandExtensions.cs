using System;
using System.ComponentModel;
using System.Windows;

namespace FirstFloor.ModernUI.Commands {
    // based on http://stackoverflow.com/a/1857619/4267982
    public static class DelegateCommandExtensions {
        /// <summary>
        /// Makes DelegateCommnand listen on PropertyChanged events of CommandBase object,
        /// so that DelegateCommand can update its IsEnabled property.
        /// </summary>
        public static TObj ListenOn<TObj>(this TObj delegateCommand, CommandBase observedCommand) where TObj : CommandBase {
            return delegateCommand.ListenOn(observedCommand, nameof(CommandBase.IsAbleToExecute));
        }

        /// <summary>
        /// Makes DelegateCommnand listen on PropertyChanged events of CommandBase object,
        /// so that DelegateCommand can update its IsEnabled property.
        /// </summary>
        public static TObj ListenOnWeak<TObj>(this TObj delegateCommand, CommandBase observedCommand) where TObj : CommandBase {
            return delegateCommand.ListenOnWeak(observedCommand, nameof(CommandBase.IsAbleToExecute));
        }

        /// <summary>
        /// Makes DelegateCommnand listen on PropertyChanged events of some object,
        /// so that DelegateCommand can update its IsEnabled property.
        /// </summary>
        public static TObj ListenOn<T, TObj>(this TObj delegateCommand, T observedObject, string propertyName) where T : INotifyPropertyChanged
                where TObj : CommandBase {
            observedObject.PropertyChanged += (sender, e) => {
                if (e.PropertyName == propertyName) {
                    delegateCommand.RaiseCanExecuteChanged();
                }
            };
            return delegateCommand;
        }

        /// <summary>
        /// Makes DelegateCommnand listen on PropertyChanged events of some object,
        /// so that DelegateCommand can update its IsEnabled property.
        /// </summary>
        public static TObj ListenOnWeak<T, TObj>(this TObj delegateCommand, T observedObject, string propertyName) where T : INotifyPropertyChanged
                where TObj : CommandBase {
            WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.AddHandler(observedObject, nameof(INotifyPropertyChanged.PropertyChanged),
                    (sender, e) => {
                        if (e.PropertyName == propertyName) {
                            delegateCommand.RaiseCanExecuteChanged();
                        }
                    });
            return delegateCommand;
        }

        /// <summary>
        /// Makes DelegateCommnand listen on PropertyChanged events of some object,
        /// so that DelegateCommand can update its IsEnabled property.
        /// </summary>
        public static TObj ListenOn<T, TObj>(this TObj delegateCommand, T observedObject, params string[] propertyNames) where T : INotifyPropertyChanged
                where TObj : CommandBase {
            observedObject.PropertyChanged += (sender, e) => {
                if (Array.IndexOf(propertyNames, e.PropertyName) != -1) {
                    delegateCommand.RaiseCanExecuteChanged();
                }
            };
            return delegateCommand;
        }

        /// <summary>
        /// Makes DelegateCommnand listen on PropertyChanged events of some object,
        /// so that DelegateCommand can update its IsEnabled property.
        /// </summary>
        public static TObj ListenOnWeak<T, TObj>(this TObj delegateCommand, T observedObject, params string[] propertyNames) where T : INotifyPropertyChanged
                where TObj : CommandBase {
            WeakEventManager<INotifyPropertyChanged, PropertyChangedEventArgs>.AddHandler(observedObject, nameof(INotifyPropertyChanged.PropertyChanged),
                    (sender, e) => {
                        if (Array.IndexOf(propertyNames, e.PropertyName) != -1) {
                            delegateCommand.RaiseCanExecuteChanged();
                        }
                    });
            return delegateCommand;
        }

        public static DelegateCommand Bind<T>(this DelegateCommand<T> command, T parameter) {
            return new DelegateCommand(() => command.Execute(parameter), () => command.CanExecute(parameter)).ListenOnWeak(command);
        }

        public static AsyncCommand Bind<T>(this AsyncCommand<T> command, T parameter) {
            return new AsyncCommand(() => command.ExecuteAsync(parameter), () => command.CanExecute(parameter)).ListenOnWeak(command);
        }
    }
}