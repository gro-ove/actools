using System;
using System.Windows.Input;
using FirstFloor.ModernUI.Commands;
using FirstFloor.ModernUI.Helpers;

namespace AcManager.Tools.AcObjectsNew {
    public abstract partial class AcCommonObject {
        private CommandBase _viewInExplorerCommand;

        public virtual ICommand ViewInExplorerCommand => _viewInExplorerCommand ?? (_viewInExplorerCommand = new DelegateCommand(ViewInExplorer));

        private CommandBase _copyIdCommand;

        public ICommand CopyIdCommand => _copyIdCommand ?? (_copyIdCommand = new DelegateCommand<string>(o => {
            switch (o) {
                case "name":
                    ClipboardHelper.SetText(DisplayName);
                    break;

                case "path":
                    ClipboardHelper.SetText(Location);
                    break;

                default:
                    ClipboardHelper.SetText(Id);
                    break;
            }
        }));

        private CommandBase _changeIdCommand;

        public virtual ICommand ChangeIdCommand => _changeIdCommand ??
                (_changeIdCommand = new AsyncCommand<string>(RenameAsync, o => !string.IsNullOrWhiteSpace(o)));

        private ICommand _cloneCommand;

        public ICommand CloneCommand => _cloneCommand ??
                (_cloneCommand = new AsyncCommand<string>(CloneAsync, o => !string.IsNullOrWhiteSpace(o)));

        private CommandBase _toggleCommand;

        public virtual ICommand ToggleCommand => _toggleCommand ??
                (_toggleCommand = new AsyncCommand(ToggleAsync));

        private CommandBase _deleteCommand;

        public virtual ICommand DeleteCommand => _deleteCommand ??
                (_deleteCommand = new AsyncCommand(DeleteAsync));

        private CommandBase _reloadCommand;

        public virtual ICommand ReloadCommand => _reloadCommand ?? (_reloadCommand = new DelegateCommand<string>(o => {
            try {
                if (o == @"full") {
                    Manager.Reload(Id);
                } else {
                    Reload();
                }
            } catch (Exception e) {
                NonfatalError.Notify("Can’t reload", e);
            }
        }));

        private AsyncCommand _saveCommand;

        public virtual AsyncCommand SaveCommand => _saveCommand ?? (_saveCommand = new AsyncCommand(SaveAsync, () => Changed));
    }
}
