using System;
using System.Windows.Input;
using FirstFloor.ModernUI.Serialization;
using FirstFloor.ModernUI.Windows.Navigation;
using JetBrains.Annotations;

namespace FirstFloor.ModernUI.Commands {
    public class SimpleLinkCommand : ICommand, ICommandWithToolTip {
        [NotNull]
        private readonly Action _execute;

        public SimpleLinkCommand([NotNull] Action execute, string toolTip = null) {
            ToolTip = toolTip;
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }

        public bool CanExecute(object parameter) {
            return true;
        }
        public void Execute(object parameter) {
            _execute.Invoke();
        }
        
        public event EventHandler CanExecuteChanged {
            add { }
            remove { }
        }

        public string ToolTip { get; }
    }
    
    public class SimpleLinkCommand<T> : ICommand, ICommandWithToolTip {
        [NotNull]
        private readonly Action<T> _execute;

        public SimpleLinkCommand([NotNull] Action<T> execute, string toolTip = null) {
            ToolTip = toolTip;
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
        }

        public bool CanExecute(object parameter) {
            return true;
        }
        public void Execute(object parameter) {
            _execute.Invoke(parameter.As<T>());
        }
        
        public event EventHandler CanExecuteChanged {
            add { }
            remove { }
        }

        public string ToolTip { get; }
    }
}